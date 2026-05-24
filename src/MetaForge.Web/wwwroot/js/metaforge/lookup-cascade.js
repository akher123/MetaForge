/**
 * Cascading lookup engine — shared by master forms and detail line grids.
 */
const MetaForgeLookups = (function () {
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

    function buildLookupUrl(entity, filterField, filterValue) {
        let url = `/api/metaforge/lookups/${encodeURIComponent(entity)}`;
        if (filterField && filterValue != null && filterValue !== '') {
            url += `?filterField=${encodeURIComponent(filterField)}&filterValue=${encodeURIComponent(filterValue)}`;
        }
        return url;
    }

    function populateSelect($select, items, selectedValue, useSelect2) {
        $select.empty().append('<option value="">-- Select --</option>');
        (items || []).forEach(i => {
            const val = i.Value ?? i.value;
            const text = i.Text ?? i.text;
            $select.append(`<option value="${val}">${escapeHtml(text)}</option>`);
        });

        if (selectedValue != null && selectedValue !== '') {
            $select.val(String(selectedValue));
        }

        if (useSelect2 && $select.hasClass('select2-hidden-accessible')) {
            $select.select2('destroy');
        }

        if (useSelect2) {
            $select.select2({ theme: 'bootstrap-5', width: '100%' });
        }
    }

    function clearSelect($select, useSelect2, disabled) {
        populateSelect($select, [], null, useSelect2);
        if (disabled) {
            $select.prop('disabled', true);
        }
    }

    function loadLookup($select, entity, options) {
        const opts = options || {};
        const useSelect2 = opts.useSelect2 === true;
        const filterField = opts.filterField;
        const filterValue = opts.filterValue;
        const selectedValue = opts.selectedValue;

        if (opts.cascadeParent && (filterValue == null || filterValue === '')) {
            clearSelect($select, useSelect2, opts.disableWhenEmpty !== false);
            return $.when();
        }

        $select.prop('disabled', !!opts.disabled);

        return $.ajax({
            url: buildLookupUrl(entity, filterField, filterValue),
            dataType: 'json',
            cache: false
        }).then(items => {
            populateSelect($select, items, selectedValue, useSelect2);
            if (opts.disabled) {
                $select.prop('disabled', true);
            }
        }).fail(function () {
            clearSelect($select, useSelect2, false);
        });
    }

    function getDependentFields(fields, parentName) {
        return fields.filter(f => getParentField(f) === parentName);
    }

    function clearCascadeChain($scope, fields, parentName, resolveSelect) {
        getDependentFields(fields, parentName).forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $child = resolveSelect(name);
            if ($child.length) {
                clearSelect($child, $child.hasClass('lookup-select') && !$child.hasClass('form-select-sm'), true);
            }
            clearCascadeChain($scope, fields, name, resolveSelect);
        });
    }

    function bindFormCascade($form, fields) {
        $form.off('change.cascade', '.lookup-select');

        fields.filter(f => getParentField(f)).forEach(field => {
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
            const $child = $form.find(`select[name="${name}"]`);
            if ($child.length === 0) return;

            const previous = preserveValues ? $child.val() : null;
            if (!preserveValues) {
                $child.val('');
            }

            clearCascadeChain($form, fields, name, n => $form.find(`select[name="${n}"]`));

            loadLookup($child, getEntity(field), {
                filterField: getFilterField(field),
                filterValue: parentVal,
                selectedValue: previous,
                cascadeParent: true,
                useSelect2: true
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
        const dropdownFields = fields.filter(f => (f.ControlType ?? f.controlType) === 'Dropdown');
        const independent = dropdownFields.filter(f => !getParentField(f));
        const dependent = dropdownFields
            .filter(f => getParentField(f))
            .sort((a, b) => (a.DisplayOrder ?? a.displayOrder ?? 0) - (b.DisplayOrder ?? b.displayOrder ?? 0));

        const independentLoads = independent.map(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $sel = $form.find(`select[name="${name}"]`);
            if ($sel.length === 0) return $.when();
            return loadLookup($sel, getEntity(field), {
                useSelect2: true,
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
                const $sel = $form.find(`select[name="${name}"]`);
                const parentVal = getPendingValue(pendingValues, parentName)
                    ?? $form.find(`[name="${parentName}"]`).val();
                const selected = getPendingValue(pendingValues, name);

                loadLookup($sel, getEntity(field), {
                    filterField: getFilterField(field),
                    filterValue: parentVal,
                    selectedValue: selected,
                    cascadeParent: true,
                    useSelect2: true
                }).always(() => loadNext(index + 1));
            })(0);
        });

        return chain.promise();
    }

    function initGridLookups($container, fields, getRowValues) {
        $container.find('.lookup-select').each(function () {
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
            const useSelect2 = false;

            loadLookup($sel, entity, {
                filterField: parentName ? filterField : null,
                filterValue: parentVal,
                selectedValue: currentVal,
                cascadeParent: !!parentName,
                useSelect2,
                disableWhenEmpty: !!parentName
            });
        });

        $container.off('change.cascade', '.lookup-select').on('change.cascade', '.lookup-select', function () {
            const $parent = $(this);
            const parentField = $parent.data('field');
            const index = $parent.data('index');
            const parentVal = $parent.val();

            getDependentFields(fields, parentField).forEach(field => {
                const name = field.PropertyName ?? field.propertyName;
                const $child = $container.find(`.lookup-select[data-field="${name}"][data-index="${index}"]`);
                if ($child.length === 0) return;

                loadLookup($child, getEntity(field), {
                    filterField: getFilterField(field),
                    filterValue: parentVal,
                    cascadeParent: true,
                    useSelect2: false,
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
        cascadeAttrs,
        getEntity,
        getParentField,
        getFilterField
    };
})();
