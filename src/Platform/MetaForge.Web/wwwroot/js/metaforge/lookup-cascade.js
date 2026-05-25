/**
 * Cascading lookup engine — shared by master forms and detail line grids.
 * All lookup controls load data via paged search (10 items per request).
 */
const MetaForgeLookups = (function () {
    const LOOKUP_PAGE_SIZE = 10;

    function isLookupControlType(controlType) {
        return controlType === 'Dropdown' || controlType === 'Autocomplete';
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

    function destroySelect2($select) {
        if ($select.hasClass('select2-hidden-accessible')) {
            $select.select2('destroy');
        }
    }

    function destroyFormLookups($scope) {
        ($scope?.jquery ? $scope : $($scope)).find('.lookup-select, .lookup-autocomplete').each(function () {
            destroySelect2($(this));
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
        const disabled = !!opts.disabled;
        const placeholder = opts.placeholder || '-- Search --';

        if (!entity) {
            clearSelect($select, disabled);
            return $.when();
        }

        destroySelect2($select);
        $select.empty().append('<option value=""></option>');
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
            allowClear: true,
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

    function loadLookup($select, field, options) {
        const opts = options || {};
        const entity = resolveEntity(field, $select, opts.entity);
        const filterField = opts.filterField;
        const filterValue = opts.filterValue;
        const selectedValue = opts.selectedValue;

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
                selectedValue,
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

    function clearCascadeChain($scope, fields, parentName, resolveSelect) {
        getDependentFields(fields, parentName).forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $child = resolveSelect(name);
            if ($child.length) {
                clearSelect($child, true);
            }
            clearCascadeChain($scope, fields, name, resolveSelect);
        });
    }

    function bindFormCascade($form, fields) {
        $form.off('change.cascade', '.lookup-select, .lookup-autocomplete');

        getLookupFields(fields).filter(f => getParentField(f)).forEach(field => {
            const parentName = getParentField(field);
            const $parent = $form.find(`[name="${parentName}"]`);
            if ($parent.length === 0) return;

            $parent.on('change.cascade', function () {
                const parentVal = $(this).val();
                refreshFormDependents($form, fields, parentName, parentVal);
            });
        });
    }

    function refreshFormDependents($form, fields, parentName, parentVal, preserveValues) {
        const dependents = getDependentFields(fields, parentName);
        dependents.forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $child = resolveLookupSelect($form, name);
            if ($child.length === 0) return;

            const previous = preserveValues ? $child.val() : null;
            if (!preserveValues) {
                $child.val('');
            }

            clearCascadeChain($form, fields, name, n => resolveLookupSelect($form, n));

            loadLookup($child, field, {
                entity: getEntity(field),
                filterField: getFilterField(field),
                filterValue: parentVal,
                selectedValue: previous,
                cascadeParent: true,
                disableWhenEmpty: true
            }).then(function () {
                refreshFormDependents($form, fields, name, $child.val(), preserveValues);
            });
        });
    }

    function getPendingValue(pendingValues, name) {
        if (!pendingValues || !name) return null;
        const val = pendingValues[name]
            ?? pendingValues[name.charAt(0).toLowerCase() + name.slice(1)]
            ?? pendingValues[name.charAt(0).toUpperCase() + name.slice(1)];
        return val == null || val === '' ? null : val;
    }

    function initFormLookups($form, fields, pendingValues) {
        destroyFormLookups($form);

        const lookupFields = getLookupFields(fields);
        const independent = lookupFields.filter(f => !getParentField(f));
        const dependent = lookupFields
            .filter(f => getParentField(f))
            .sort((a, b) => (a.DisplayOrder ?? a.displayOrder ?? 0) - (b.DisplayOrder ?? b.displayOrder ?? 0));

        const independentLoads = independent.map(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $sel = resolveLookupSelect($form, name);
            if ($sel.length === 0) return $.when();
            return loadLookup($sel, field, {
                entity: getEntity(field),
                selectedValue: getPendingValue(pendingValues, name)
            });
        });

        const chain = $.Deferred();
        $.when.apply($, independentLoads.length ? independentLoads : [$.when()]).then(function () {
            (function loadNext(index) {
                if (index >= dependent.length) {
                    bindFormCascade($form, fields);
                    chain.resolve();
                    return;
                }

                const field = dependent[index];
                const name = field.PropertyName ?? field.propertyName;
                const parentName = getParentField(field);
                const $sel = resolveLookupSelect($form, name);
                const parentVal = getPendingValue(pendingValues, parentName)
                    ?? $form.find(`[name="${parentName}"]`).val();
                const selected = getPendingValue(pendingValues, name);

                loadLookup($sel, field, {
                    entity: getEntity(field),
                    filterField: getFilterField(field),
                    filterValue: parentVal,
                    selectedValue: selected,
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
        initAutocompleteSelect: initPagedLookupSelect,
        destroyFormLookups,
        cascadeAttrs,
        getEntity,
        getParentField,
        getFilterField,
        isLookupControlType,
        isAutocompleteField,
        buildItemUrl,
        LOOKUP_PAGE_SIZE
    };
})();
