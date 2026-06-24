/**
 * Cascading lookup engine — shared by master forms and detail line grids.
 * All lookup controls load data via paged search (10 items per request).
 */
const MetaForgeLookups = (function () {
    const LOOKUP_PAGE_SIZE = 10;

    function isLookupControlType(controlType) {
        return MetaForgeControlTypes.isLookupOrMultiSelect(controlType);
    }

    function isMultiSelectField(field) {
        return MetaForgeControlTypes.isMultiSelect(field?.ControlType ?? field?.controlType);
    }

    function isAutocompleteField(field) {
        return (field?.ControlType ?? field?.controlType) === 'Autocomplete';
    }

    function usesPagedSearch(field, $select) {
        if (!$select || $select.length === 0) {
            return isAutocompleteField(field);
        }
        return $select.hasClass('lookup-autocomplete') || isAutocompleteField(field) || $select.hasClass('lookup-select');
    }

    function getEntity(field) {
        const name = field.PropertyName ?? field.propertyName ?? '';
        return field.LookupEntity ?? field.lookupEntity ?? (name.endsWith('Id') ? name.replace(/Id$/, '') : '');
    }

    function getParentField(field) {
        return field.LookupParentField ?? field.lookupParentField ?? null;
    }

    function getFilterField(field) {
        return field.LookupFilterField ?? field.lookupFilterField ?? getParentField(field);
    }

    function buildSearchUrl(entity) {
        return `/api/metaforge/lookups/${encodeURIComponent(entity)}/search`;
    }

    function buildItemUrl(entity, value) {
        return `/api/metaforge/lookups/${encodeURIComponent(entity)}/item/${encodeURIComponent(value)}`;
    }

    function buildItemsUrl(entity, values) {
        const list = Array.isArray(values) ? values : [values];
        const query = list.filter(v => v != null && v !== '').join(',');
        return `/api/metaforge/lookups/${encodeURIComponent(entity)}/items?values=${encodeURIComponent(query)}`;
    }

    function resolveFormRoot($scope) {
        if (!$scope || $scope.length === 0) return $scope;
        const $form = $scope.closest('form');
        if ($form.length) return $form;
        const $shell = $scope.closest('.module-form-shell, .dynamic-form-tabbed-shell, .admin-form-preview-layout, .master-detail-form');
        if ($shell.length) return $shell;
        return $scope;
    }

    function destroySelect2($select) {
        if ($select.hasClass('select2-hidden-accessible')) {
            $select.select2('destroy');
        }
    }

    function destroyFormLookups($scope) {
        ($scope?.jquery ? $scope : $($scope)).find('.lookup-select, .lookup-autocomplete').each(function () {
            destroySelect2($(this));
        });
        ($scope?.jquery ? $scope : $($scope)).find('.lookup-multiselect-checkboxes').each(function () {
            closeMultiSelectPanel($(this));
            clearMultiSelectCheckboxes($(this), false);
        });
    }

    function resolveLookupMultiSelect($scope, name) {
        return $scope.find(`.lookup-multiselect-checkboxes[data-field-name="${name}"]`);
    }

    function resolveLookupControl($scope, name, field) {
        if (field && isMultiSelectField(field)) {
            return resolveLookupMultiSelect($scope, name);
        }
        return resolveLookupSelect($scope, name);
    }

    function readMultiSelectValues($container) {
        if (!$container || $container.length === 0) return [];
        return $container.find('.lookup-multiselect-item:checked')
            .map(function () {
                return parseInt($(this).val(), 10);
            })
            .get()
            .filter(v => !Number.isNaN(v) && v > 0);
    }

    function getMultiSelectState($container) {
        let state = $container.data('multiselectState');
        if (!state) {
            state = { selected: {}, options: {}, skip: 0, hasMore: false, search: '' };
            $container.data('multiselectState', state);
        }
        return state;
    }

    function syncMultiSelectSummary($container) {
        const state = getMultiSelectState($container);
        const selected = Object.entries(state.selected).sort((a, b) => a[1].localeCompare(b[1]));
        const $summary = $container.find('.lookup-multiselect-summary');
        const $toggle = $container.find('.lookup-multiselect-toggle');

        if (selected.length === 0) {
            $summary.text('Select...').addClass('text-muted');
        } else if (selected.length === 1) {
            $summary.text(selected[0][1]).removeClass('text-muted');
        } else {
            const labels = selected.map(([, text]) => text).join(', ');
            $summary.text(labels.length > 52 ? `${selected.length} selected` : labels).removeClass('text-muted');
        }

        $toggle.attr('aria-label', selected.length ? `${selected.length} selected` : 'Select options');
    }

    function resetMultiSelectPanelPosition($container) {
        $container.find('.lookup-multiselect-panel').css({
            position: '',
            top: '',
            left: '',
            width: '',
            right: '',
            bottom: '',
            zIndex: '',
            maxHeight: '',
            overflow: ''
        });
    }

    function positionMultiSelectPanel($container) {
        const $panel = $container.find('.lookup-multiselect-panel');
        const $toggle = $container.find('.lookup-multiselect-toggle');
        if ($panel.length === 0 || $toggle.length === 0 || !$container.hasClass('lookup-multiselect-open')) {
            return;
        }

        const rect = $toggle[0].getBoundingClientRect();
        const viewportPadding = 8;
        const spaceBelow = window.innerHeight - rect.bottom - viewportPadding;
        const spaceAbove = rect.top - viewportPadding;
        const preferBelow = spaceBelow >= 160 || spaceBelow >= spaceAbove;

        $panel.css({
            position: 'fixed',
            left: `${Math.round(rect.left)}px`,
            width: `${Math.round(rect.width)}px`,
            right: 'auto',
            bottom: 'auto',
            zIndex: 1065
        });

        if (preferBelow) {
            $panel.css({
                top: `${Math.round(rect.bottom + 2)}px`,
                maxHeight: `${Math.max(120, Math.min(320, spaceBelow))}px`,
                overflow: 'auto'
            });
        } else {
            const maxHeight = Math.max(120, Math.min(320, spaceAbove));
            $panel.css({
                top: `${Math.round(rect.top - maxHeight - 2)}px`,
                maxHeight: `${maxHeight}px`,
                overflow: 'auto'
            });
        }
    }

    function bindMultiSelectRepositionEvents() {
        if ($(window).data('multiselectRepositionBound')) {
            return;
        }

        $(window).data('multiselectRepositionBound', true);
        $(window).on('resize.multiselect scroll.multiselect', function () {
            $('.lookup-multiselect-checkboxes.lookup-multiselect-open').each(function () {
                positionMultiSelectPanel($(this));
            });
        });
    }

    function closeMultiSelectPanel($container) {
        if (!$container || $container.length === 0) return;
        $container.removeClass('lookup-multiselect-open');
        $container.find('.lookup-multiselect-toggle').attr('aria-expanded', 'false');
        $container.find('.lookup-multiselect-panel').hide();
        resetMultiSelectPanelPosition($container);
    }

    function closeAllMultiSelectPanels(except) {
        ($('.lookup-multiselect-checkboxes.lookup-multiselect-open')).each(function () {
            if (!except || !$(this).is(except)) {
                closeMultiSelectPanel($(this));
            }
        });
    }

    function openMultiSelectPanel($container) {
        if (!$container || $container.length === 0 || $container.hasClass('lookup-multiselect-disabled')) {
            return;
        }

        closeAllMultiSelectPanels($container);
        $container.addClass('lookup-multiselect-open');
        $container.find('.lookup-multiselect-toggle').attr('aria-expanded', 'true');
        $container.find('.lookup-multiselect-panel').show();
        bindMultiSelectRepositionEvents();
        positionMultiSelectPanel($container);
        window.setTimeout(function () {
            positionMultiSelectPanel($container);
            $container.find('.lookup-multiselect-search').trigger('focus');
        }, 0);
    }

    function bindMultiSelectDropdownGlobalEvents() {
        if ($(document).data('multiselectDropdownBound')) {
            return;
        }

        $(document).data('multiselectDropdownBound', true);
        $(document).on('click.multiselectDropdown', function (e) {
            if ($(e.target).closest('.lookup-multiselect-checkboxes').length) {
                return;
            }
            closeAllMultiSelectPanels();
        });
        $(document).on('keydown.multiselectDropdown', function (e) {
            if (e.key === 'Escape') {
                closeAllMultiSelectPanels();
            }
        });
    }

    function setMultiSelectDisabled($container, disabled) {
        $container.toggleClass('lookup-multiselect-disabled', !!disabled);
        $container.find('.lookup-multiselect-toggle, .lookup-multiselect-search, .lookup-multiselect-item, .lookup-multiselect-load-more')
            .prop('disabled', !!disabled);
        if (disabled) {
            closeMultiSelectPanel($container);
        }
    }

    function renderMultiSelectOptions($container) {
        const state = getMultiSelectState($container);
        const $list = $container.find('.lookup-multiselect-list');
        const merged = new Map();

        Object.entries(state.selected).forEach(([value, text]) => {
            merged.set(String(value), text);
        });
        Object.entries(state.options).forEach(([value, text]) => {
            merged.set(String(value), text);
        });

        $list.empty();
        if (merged.size === 0) {
            $list.append('<div class="lookup-multiselect-no-results small text-muted px-2 py-2">No matches found.</div>');
            return;
        }

        Array.from(merged.entries())
            .sort((a, b) => a[1].localeCompare(b[1]))
            .forEach(([value, text]) => {
                const checked = Object.prototype.hasOwnProperty.call(state.selected, value) ? 'checked' : '';
                $list.append(`
                    <label class="lookup-multiselect-option form-check" role="option">
                        <input type="checkbox" class="form-check-input lookup-multiselect-item" value="${escapeAttr(value)}" ${checked} />
                        <span class="lookup-multiselect-option-label form-check-label">${escapeHtml(text)}</span>
                    </label>`);
            });

        if ($container.hasClass('lookup-multiselect-open')) {
            positionMultiSelectPanel($container);
        }
    }

    function clearMultiSelectCheckboxes($container, disabled) {
        if (!$container || $container.length === 0) return;
        closeMultiSelectPanel($container);
        $container.removeData('multiselectState');
        $container.find('.lookup-multiselect-search').val('');
        $container.find('.lookup-multiselect-list').empty();
        $container.find('.lookup-multiselect-load-more').addClass('d-none');
        $container.find('.lookup-multiselect-empty').addClass('d-none');
        $container.find('.lookup-multiselect-loading').addClass('d-none');
        syncMultiSelectSummary($container);
        setMultiSelectDisabled($container, disabled);
    }

    function fetchLookupSearch(entity, options) {
        const payload = {
            search: options.search || '',
            skip: options.skip || 0,
            take: LOOKUP_PAGE_SIZE
        };
        if (options.filterField) payload.filterField = options.filterField;
        if (options.filterValue != null && options.filterValue !== '') payload.filterValue = options.filterValue;

        return $.ajax({
            url: buildSearchUrl(entity),
            dataType: 'json',
            cache: false,
            data: payload
        });
    }

    function preloadMultiSelectSelections($container, entity, selectedValues) {
        if (!Array.isArray(selectedValues) || selectedValues.length === 0) {
            return $.when();
        }

        return $.ajax({
            url: buildItemsUrl(entity, selectedValues),
            dataType: 'json',
            cache: false
        }).then(data => {
            const state = getMultiSelectState($container);
            (data.items ?? data.Items ?? []).forEach(item => {
                const value = String(item.value ?? item.Value ?? '');
                const text = item.text ?? item.Text ?? value;
                if (value) state.selected[value] = text;
            });
            selectedValues.forEach(val => {
                const key = String(val);
                if (!state.selected[key]) state.selected[key] = key;
            });
            syncMultiSelectSummary($container);
        });
    }

    function loadMultiSelectPage($container, field, options, append) {
        const opts = options || {};
        const entity = resolveEntity(field, $container, opts.entity);
        const state = getMultiSelectState($container);
        const $loading = $container.find('.lookup-multiselect-loading');
        const $loadMore = $container.find('.lookup-multiselect-load-more');

        if (!append) {
            state.skip = 0;
            state.options = {};
            state.hasMore = false;
        }

        $loading.removeClass('d-none');
        return fetchLookupSearch(entity, {
            search: state.search,
            skip: state.skip,
            filterField: opts.filterField,
            filterValue: opts.filterValue
        }).always(() => {
            $loading.addClass('d-none');
        }).then(data => {
            const items = data.items ?? data.Items ?? [];
            items.forEach(item => {
                const value = String(item.value ?? item.Value ?? '');
                const text = item.text ?? item.Text ?? value;
                if (value) state.options[value] = text;
            });
            state.hasMore = !!(data.hasMore ?? data.HasMore);
            state.skip += items.length;
            renderMultiSelectOptions($container);
            $loadMore.toggleClass('d-none', !state.hasMore);
            syncMultiSelectSummary($container);
        });
    }

    function bindMultiSelectEvents($container, field, options) {
        const entity = resolveEntity(field, $container, options.entity);

        bindMultiSelectDropdownGlobalEvents();

        $container.off('click.multiselectToggle', '.lookup-multiselect-toggle');
        $container.off('input.multiselectSearch', '.lookup-multiselect-search');
        $container.off('click.multiselectOption', '.lookup-multiselect-option');
        $container.off('change.multiselectItem', '.lookup-multiselect-item');
        $container.off('click.multiselectLoadMore', '.lookup-multiselect-load-more');

        $container.on('click.multiselectToggle', '.lookup-multiselect-toggle', function (e) {
            e.preventDefault();
            e.stopPropagation();
            const $multi = $(this).closest('.lookup-multiselect-checkboxes');
            if ($multi.hasClass('lookup-multiselect-open')) {
                closeMultiSelectPanel($multi);
                return;
            }

            const state = getMultiSelectState($multi);
            const hasOptions = Object.keys(state.options).length > 0 || $multi.find('.lookup-multiselect-item').length > 0;
            if (!hasOptions && !$multi.hasClass('lookup-multiselect-disabled') && $multi.data('multiselectBoundEntity')) {
                loadMultiSelectPage($multi, field, options, false).then(function () {
                    openMultiSelectPanel($multi);
                });
                return;
            }

            openMultiSelectPanel($multi);
        });

        $container.on('input.multiselectSearch', '.lookup-multiselect-search', function () {
            const state = getMultiSelectState($container);
            state.search = $(this).val() || '';
            state.skip = 0;
            state.options = {};
            loadMultiSelectPage($container, field, options, false);
        });

        $container.on('click.multiselectOption', '.lookup-multiselect-option', function (e) {
            e.stopPropagation();
        });

        $container.on('change.multiselectItem', '.lookup-multiselect-item', function () {
            const state = getMultiSelectState($container);
            const value = String($(this).val());
            const text = $(this).closest('.lookup-multiselect-option').find('.lookup-multiselect-option-label').text();
            if ($(this).is(':checked')) {
                state.selected[value] = text;
            } else {
                delete state.selected[value];
            }
            syncMultiSelectSummary($container);
            $container.trigger('change');
        });

        $container.on('click.multiselectLoadMore', '.lookup-multiselect-load-more', function () {
            loadMultiSelectPage($container, field, options, true);
        });

        $container.data('multiselectBoundEntity', entity);
    }

    function initMultiSelectCheckboxes($container, field, options) {
        const opts = options || {};
        const entity = resolveEntity(field, $container, opts.entity);
        const disabled = !!opts.disabled || $container.data('disabled') === true || $container.data('disabled') === 'true';

        if (opts.cascadeParent && (opts.filterValue == null || opts.filterValue === '')) {
            clearMultiSelectCheckboxes($container, opts.disableWhenEmpty !== false);
            $container.find('.lookup-multiselect-summary').text('Select the parent field first.').addClass('text-muted');
            $container.find('.lookup-multiselect-empty').removeClass('d-none');
            return $.when();
        }

        if (!entity) {
            clearMultiSelectCheckboxes($container, disabled);
            return $.when();
        }

        $container.find('.lookup-multiselect-empty').addClass('d-none');
        clearMultiSelectCheckboxes($container, disabled);
        getMultiSelectState($container);

        const selectedValues = opts.selectedValues
            ?? (Array.isArray(opts.selectedValue) ? opts.selectedValue : null);

        return preloadMultiSelectSelections($container, entity, selectedValues)
            .then(() => loadMultiSelectPage($container, field, opts, false))
            .then(() => {
                bindMultiSelectEvents($container, field, opts);
            });
    }

    function resolveEntity(field, $select, explicitEntity) {
        return explicitEntity || $select.data('lookup') || (field ? getEntity(field) : '');
    }

    function getDropdownParent($select) {
        const $modal = $select.closest('.modal');
        if ($modal.length) return $modal;
        const $preview = $select.closest('.form-builder-preview-body, .admin-form-preview');
        if ($preview.length) return $preview;
        const $cell = $select.closest('td');
        if ($cell.length) return $cell;
        return $(document.body);
    }

    function clearSelect($select, disabled) {
        destroySelect2($select);
        $select.empty().append('<option value=""></option>');
        if (disabled) {
            $select.prop('disabled', true);
        }
    }

    function initPagedLookupSelect($select, entity, options) {
        const opts = options || {};
        const filterField = opts.filterField || null;
        const filterValue = opts.filterValue ?? null;
        const selectedValue = opts.selectedValue;
        const selectedValues = opts.selectedValues;
        const disabled = !!opts.disabled;
        const placeholder = opts.placeholder || '-- Search --';
        const isMultiple = !!opts.multiple || $select.prop('multiple');

        if (!entity) {
            clearSelect($select, disabled);
            return $.when();
        }

        destroySelect2($select);
        $select.empty();
        if (!isMultiple) {
            $select.append('<option value=""></option>');
        }
        $select.prop('disabled', disabled);

        const ajaxData = params => {
            const payload = {
                search: params.term || '',
                skip: ((params.page || 1) - 1) * LOOKUP_PAGE_SIZE,
                take: LOOKUP_PAGE_SIZE
            };
            if (filterField) payload.filterField = filterField;
            if (filterValue != null && filterValue !== '') payload.filterValue = filterValue;
            return payload;
        };

        $select.select2({
            theme: 'bootstrap-5',
            width: '100%',
            placeholder,
            allowClear: !isMultiple,
            multiple: isMultiple,
            minimumInputLength: 0,
            dropdownParent: getDropdownParent($select),
            language: {
                inputTooShort: () => 'Type to search...',
                searching: () => 'Loading...'
            },
            ajax: {
                url: buildSearchUrl(entity),
                dataType: 'json',
                delay: 250,
                cache: true,
                data: ajaxData,
                processResults: (data, params) => {
                    params.page = params.page || 1;
                    const items = data.items ?? data.Items ?? [];
                    return {
                        results: items.map(i => ({
                            id: i.value ?? i.Value,
                            text: i.text ?? i.Text
                        })),
                        pagination: { more: !!(data.hasMore ?? data.HasMore) }
                    };
                }
            }
        });

        if (Array.isArray(selectedValues) && selectedValues.length > 0) {
            return $.ajax({
                url: buildItemsUrl(entity, selectedValues),
                dataType: 'json',
                cache: false
            }).then(data => {
                const items = data.items ?? data.Items ?? [];
                items.forEach(item => {
                    const val = item.value ?? item.Value;
                    const text = item.text ?? item.Text;
                    const option = new Option(text, val, true, true);
                    $select.append(option);
                });
                $select.trigger('change');
            }).fail(() => {
                selectedValues.forEach(val => {
                    const option = new Option(String(val), String(val), true, true);
                    $select.append(option);
                });
                $select.trigger('change');
            });
        }

        if (selectedValue != null && selectedValue !== '') {
            return $.ajax({
                url: buildItemUrl(entity, selectedValue),
                dataType: 'json',
                cache: false
            }).then(item => {
                if (!item) return;
                const val = item.Value ?? item.value;
                const text = item.Text ?? item.text;
                const option = new Option(text, val, true, true);
                $select.append(option).trigger('change');
            }).fail(() => {
                $select.val(String(selectedValue)).trigger('change');
            });
        }

        return $.when();
    }

    function loadLookup($control, field, options) {
        if (($control.hasClass && $control.hasClass('lookup-multiselect-checkboxes')) || isMultiSelectField(field)) {
            return initMultiSelectCheckboxes($control, field, options);
        }

        const $select = $control;
        const opts = options || {};
        const entity = resolveEntity(field, $select, opts.entity);
        const filterField = opts.filterField;
        const filterValue = opts.filterValue;
        const selectedValue = opts.selectedValue;
        const selectedValues = opts.selectedValues ?? (Array.isArray(selectedValue) ? selectedValue : null);

        if (opts.cascadeParent && (filterValue == null || filterValue === '')) {
            clearSelect($select, opts.disableWhenEmpty !== false);
            return $.when();
        }

        if (!entity) {
            clearSelect($select, false);
            return $.when();
        }

        if (usesPagedSearch(field, $select)) {
            return initPagedLookupSelect($select, entity, {
                filterField,
                filterValue,
                selectedValue: Array.isArray(selectedValue) ? null : selectedValue,
                disabled: opts.disabled,
                placeholder: isAutocompleteField(field) || $select.hasClass('lookup-autocomplete')
                    ? '-- Search --'
                    : '-- Select --'
            });
        }

        clearSelect($select, !!opts.disabled);
        return $.when();
    }

    function getDependentFields(fields, parentName) {
        return fields.filter(f => getParentField(f) === parentName && isLookupControlType(f.ControlType ?? f.controlType));
    }

    function getLookupFields(fields) {
        return fields.filter(f => isLookupControlType(f.ControlType ?? f.controlType));
    }

    function resolveLookupSelect($scope, name) {
        return $scope.find(`select[name="${name}"], select.lookup-select[data-field="${name}"], select.lookup-autocomplete[data-field="${name}"]`);
    }

    function resolveParentControl($scope, parentName, fields) {
        const parentField = fields.find(f => (f.PropertyName ?? f.propertyName) === parentName);
        if (parentField && isMultiSelectField(parentField)) {
            return resolveLookupMultiSelect($scope, parentName);
        }
        return resolveLookupSelect($scope, parentName);
    }

    function readParentFieldValue($scope, parentName, fields) {
        const $parent = resolveParentControl($scope, parentName, fields);
        if ($parent.length === 0) return null;

        if ($parent.hasClass('lookup-multiselect-checkboxes')) {
            const values = readMultiSelectValues($parent);
            return values.length > 0 ? values[0] : null;
        }

        const val = $parent.val();
        return val == null || val === '' ? null : val;
    }

    function cascadeFilterValue(value) {
        if (Array.isArray(value)) {
            return value.length > 0 ? value[0] : null;
        }
        return value == null || value === '' ? null : value;
    }

    function clearCascadeChain($scope, fields, parentName, resolveControl) {
        getDependentFields(fields, parentName).forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $child = resolveControl(name, field);
            if ($child.length === 0) return;
            if ($child.hasClass('lookup-multiselect-checkboxes')) {
                clearMultiSelectCheckboxes($child, true);
                $child.find('.lookup-multiselect-summary').text('Select the parent field first.').addClass('text-muted');
                $child.find('.lookup-multiselect-empty').removeClass('d-none');
            } else {
                clearSelect($child, true);
            }
            clearCascadeChain($scope, fields, name, resolveControl);
        });
    }

    function bindFormCascade($form, fields) {
        $form.off(
            'change.cascade select2:select.cascade select2:clear.cascade',
            '.lookup-select, .lookup-autocomplete, .lookup-multiselect-checkboxes'
        );
        $form.on(
            'change.cascade select2:select.cascade select2:clear.cascade',
            '.lookup-select, .lookup-autocomplete, .lookup-multiselect-checkboxes',
            function () {
                const $el = $(this);
                let parentName;
                let parentVal;

                if ($el.hasClass('lookup-multiselect-checkboxes')) {
                    parentName = $el.data('fieldName');
                    parentVal = cascadeFilterValue(readMultiSelectValues($el));
                } else {
                    parentName = $el.attr('name');
                    parentVal = $el.val();
                }

                if (!parentName) return;

                const hasDependents = getLookupFields(fields).some(f => getParentField(f) === parentName);
                if (!hasDependents) return;

                refreshFormDependents($form, fields, parentName, parentVal);
            }
        );
    }

    function refreshFormDependents($form, fields, parentName, parentVal, preserveValues) {
        const dependents = getDependentFields(fields, parentName);
        dependents.forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $child = resolveLookupControl($form, name, field);
            if ($child.length === 0) return;

            let previous = null;
            if (preserveValues) {
                previous = $child.hasClass('lookup-multiselect-checkboxes')
                    ? readMultiSelectValues($child)
                    : $child.val();
            } else if ($child.hasClass('lookup-multiselect-checkboxes')) {
                clearMultiSelectCheckboxes($child, false);
            } else if ($child.prop('multiple')) {
                $child.val([]).trigger('change');
            } else {
                $child.val('');
            }

            clearCascadeChain($form, fields, name, (childName, childField) =>
                resolveLookupControl($form, childName, childField ?? fields.find(f => (f.PropertyName ?? f.propertyName) === childName)));

            loadLookup($child, field, {
                entity: getEntity(field),
                filterField: getFilterField(field),
                filterValue: parentVal,
                selectedValue: previous,
                selectedValues: Array.isArray(previous) ? previous : null,
                cascadeParent: true,
                disableWhenEmpty: true
            }).then(function () {
                const childVal = $child.hasClass('lookup-multiselect-checkboxes')
                    ? readMultiSelectValues($child)
                    : $child.val();
                refreshFormDependents($form, fields, name, cascadeFilterValue(childVal), preserveValues);
            });
        });
    }

    function getPendingValue(pendingValues, name) {
        if (!pendingValues || !name) return null;
        const val = pendingValues[name]
            ?? pendingValues[name.charAt(0).toLowerCase() + name.slice(1)]
            ?? pendingValues[name.charAt(0).toUpperCase() + name.slice(1)];
        if (val == null || val === '') return null;
        if (Array.isArray(val)) return val;
        return val;
    }

    function getPendingSelectedValues(pendingValues, name, field) {
        const val = getPendingValue(pendingValues, name);
        if (Array.isArray(val)) return val;
        if (isMultiSelectField(field) && val != null) return [val];
        return null;
    }

    function initFormLookups($form, fields, pendingValues) {
        const $root = resolveFormRoot($form);
        destroyFormLookups($root);

        const lookupFields = getLookupFields(fields);
        const independent = lookupFields.filter(f => !getParentField(f));
        const dependent = lookupFields
            .filter(f => getParentField(f))
            .sort((a, b) => (a.DisplayOrder ?? a.displayOrder ?? 0) - (b.DisplayOrder ?? b.displayOrder ?? 0));

        const independentLoads = independent.map(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $control = resolveLookupControl($root, name, field);
            if ($control.length === 0) return $.when();
            return loadLookup($control, field, {
                entity: getEntity(field),
                selectedValue: getPendingSelectedValues(pendingValues, name, field) ? null : getPendingValue(pendingValues, name),
                selectedValues: getPendingSelectedValues(pendingValues, name, field)
            });
        });

        const chain = $.Deferred();
        $.when.apply($, independentLoads.length ? independentLoads : [$.when()]).then(function () {
            (function loadNext(index) {
                if (index >= dependent.length) {
                    bindFormCascade($root, fields);
                    chain.resolve();
                    return;
                }

                const field = dependent[index];
                const name = field.PropertyName ?? field.propertyName;
                const parentName = getParentField(field);
                const $control = resolveLookupControl($root, name, field);
                const parentVal = getPendingValue(pendingValues, parentName)
                    ?? readParentFieldValue($root, parentName, fields);
                const selected = getPendingValue(pendingValues, name);
                const selectedValues = getPendingSelectedValues(pendingValues, name, field);

                loadLookup($control, field, {
                    entity: getEntity(field),
                    filterField: getFilterField(field),
                    filterValue: parentVal,
                    selectedValue: selectedValues ? null : selected,
                    selectedValues,
                    cascadeParent: true,
                    disableWhenEmpty: true
                }).always(() => loadNext(index + 1));
            })(0);
        });

        return chain.promise();
    }

    function initGridLookups($container, fields, getRowValues) {
        $container.find('.lookup-select, .lookup-autocomplete').each(function () {
            const $sel = $(this);
            const entity = $sel.data('lookup');
            const fieldName = $sel.data('field');
            const index = $sel.data('index');
            const field = fields.find(f => (f.PropertyName ?? f.propertyName) === fieldName);
            const rowValues = getRowValues(index) || {};
            const parentName = $sel.data('cascadeParent') || getParentField(field);
            const filterField = $sel.data('cascadeFilter') || getFilterField(field);
            const parentVal = parentName ? rowValues[parentName] : null;
            const currentVal = rowValues[fieldName];

            loadLookup($sel, field, {
                entity,
                filterField: parentName ? filterField : null,
                filterValue: parentVal,
                selectedValue: currentVal,
                cascadeParent: !!parentName,
                disableWhenEmpty: !!parentName
            });
        });

        $container.off('change.cascade', '.lookup-select, .lookup-autocomplete')
            .on('change.cascade', '.lookup-select, .lookup-autocomplete', function () {
                const $parent = $(this);
                const parentField = $parent.data('field');
                const index = $parent.data('index');
                const parentVal = $parent.val();

                getDependentFields(fields, parentField).forEach(field => {
                    const name = field.PropertyName ?? field.propertyName;
                    const $child = $container.find(
                        `.lookup-select[data-field="${name}"][data-index="${index}"], .lookup-autocomplete[data-field="${name}"][data-index="${index}"]`
                    );
                    if ($child.length === 0) return;

                    loadLookup($child, field, {
                        entity: getEntity(field),
                        filterField: getFilterField(field),
                        filterValue: parentVal,
                        cascadeParent: true,
                        disableWhenEmpty: true
                    });
                });
            });
    }

    function cascadeAttrs(field) {
        const parent = getParentField(field);
        if (!parent) return '';
        const filter = getFilterField(field);
        return `data-cascade-parent="${escapeAttr(parent)}" data-cascade-filter="${escapeAttr(filter || parent)}"`;
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/'/g, '&#39;');
    }

    return {
        loadLookup,
        initFormLookups,
        initGridLookups,
        initPagedLookupSelect,
        initMultiSelectCheckboxes,
        closeMultiSelectPanel,
        initAutocompleteSelect: initPagedLookupSelect,
        destroyFormLookups,
        cascadeAttrs,
        getEntity,
        getParentField,
        getFilterField,
        isLookupControlType,
        isMultiSelectField,
        isAutocompleteField,
        readMultiSelectValues,
        readParentFieldValue,
        resolveLookupControl,
        buildItemUrl,
        buildItemsUrl,
        LOOKUP_PAGE_SIZE
    };
})();
