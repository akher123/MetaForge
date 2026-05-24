/**
 * Dynamic Form Engine - renders forms from metadata.
 */
const DynamicForm = (function () {
    let $form, formDef, recordId = null;

    function getFields() {
        return formDef?.Fields ?? formDef?.fields ?? [];
    }

    function getField(name) {
        return getFields().find(f => (f.PropertyName ?? f.propertyName) === name);
    }

    function init(selector, definition, options) {
        $form = $(selector);
        formDef = definition;
        recordId = null;
        if (options?.layoutClass) {
            $form.addClass(options.layoutClass);
        }
        render();
    }

    function render() {
        $form.empty();
        const sections = groupBySection(getFields());

        Object.keys(sections).forEach(sectionName => {
            if (sectionName !== 'default') {
                $form.append(`<div class="admin-form-section-title">${sectionName}</div>`);
            }

            sections[sectionName].forEach(field => {
                const isVisible = field.IsVisible ?? field.isVisible ?? true;
                if (!isVisible) return;
                const isRequired = field.IsRequired ?? field.isRequired;
                const label = field.Label ?? field.label;
                const col = $('<div class="admin-form-field"></div>');
                col.append(`<label class="admin-form-label">${label}${isRequired ? ' <span class="required-mark">*</span>' : ''}</label>`);
                col.append(buildControl(field));
                $form.append(col);
            });
        });
    }

    function buildControl(field) {
        const name = field.PropertyName ?? field.propertyName;
        const readonly = (field.IsReadOnly ?? field.isReadOnly) ? 'readonly' : '';
        const required = (field.IsRequired ?? field.isRequired) ? 'required' : '';
        const disabled = (field.IsReadOnly ?? field.isReadOnly) ? 'disabled' : '';
        const controlType = field.ControlType ?? field.controlType ?? 'TextBox';
        const lookupEntity = field.LookupEntity ?? field.lookupEntity;
        const cascadeAttrs = typeof MetaForgeLookups !== 'undefined' ? MetaForgeLookups.cascadeAttrs(field) : '';

        switch (controlType) {
            case 'TextArea':
                return `<textarea class="form-control admin-form-control" name="${name}" ${readonly} ${required} rows="3"></textarea>`;
            case 'Number':
                return `<input type="number" step="any" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
            case 'Date':
                return `<input type="date" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
            case 'DateTime':
                return `<input type="datetime-local" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
            case 'Checkbox':
                return `<div class="form-check mt-1"><input type="checkbox" class="form-check-input" name="${name}" ${readonly} ${disabled} /></div>`;
            case 'Dropdown':
                return `<select class="form-select admin-form-control lookup-select" name="${name}" data-lookup="${lookupEntity || (name || '').replace(/Id$/, '')}" ${cascadeAttrs} ${required} ${disabled}></select>`;
            case 'Hidden':
                return `<input type="hidden" name="${name}" />`;
            default:
                return `<input type="text" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
        }
    }

    function groupBySection(fields) {
        const groups = {};
        fields.forEach(f => {
            const key = f.SectionName ?? f.sectionName ?? 'default';
            if (!groups[key]) groups[key] = [];
            groups[key].push(f);
        });
        return groups;
    }

    function readFieldValue($el, field) {
        const controlType = field?.ControlType ?? field?.controlType ?? 'TextBox';

        if ($el.attr('type') === 'checkbox') {
            return $el.is(':checked');
        }

        const raw = $el.val();
        if (raw === '' || raw == null) {
            return null;
        }

        if (controlType === 'Number') {
            const num = parseFloat(raw);
            return Number.isNaN(num) ? null : num;
        }

        if (controlType === 'Dropdown' && (field?.PropertyName ?? field?.propertyName ?? '').endsWith('Id')) {
            const num = parseInt(raw, 10);
            return Number.isNaN(num) || num === 0 ? null : num;
        }

        return raw;
    }

    function getData() {
        const data = {};
        $form.find('[name]').each(function () {
            const $el = $(this);
            const name = $el.attr('name');
            data[name] = readFieldValue($el, getField(name));
        });
        if (recordId != null && recordId !== '') {
            data.Id = parseInt(recordId, 10);
        }
        return data;
    }

    function formatDateTimeLocal(value) {
        const dt = new Date(value);
        if (isNaN(dt.getTime())) return value;

        const pad = n => String(n).padStart(2, '0');
        return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
    }

    function normalizeDataKeys(data) {
        const normalized = {};
        Object.keys(data || {}).forEach(k => {
            normalized[k] = data[k];
            normalized[k.charAt(0).toUpperCase() + k.slice(1)] = data[k];
            normalized[k.charAt(0).toLowerCase() + k.slice(1)] = data[k];
        });
        return normalized;
    }

    function getDataValue(data, name) {
        if (!data) return undefined;
        return data[name]
            ?? data[name.charAt(0).toLowerCase() + name.slice(1)]
            ?? data[name.charAt(0).toUpperCase() + name.slice(1)];
    }

    function applyScalarFieldValues(data) {
        const id = data?.Id ?? data?.id;
        if (id != null && id !== '') {
            recordId = id;
        }

        const normalized = normalizeDataKeys(data);

        $form.find('[name]').each(function () {
            const name = $(this).attr('name');
            const value = normalized[name];
            if (value === undefined || value === null) return;

            const $el = $(this);
            if ($el.is('select')) return;

            const field = getField(name);
            const controlType = field?.ControlType ?? field?.controlType ?? 'TextBox';

            if ($el.attr('type') === 'checkbox') {
                $el.prop('checked', !!value);
            } else if (controlType === 'DateTime' && value) {
                $el.val(formatDateTimeLocal(value));
            } else if (controlType === 'Date' && value) {
                const dt = new Date(value);
                $el.val(isNaN(dt.getTime()) ? value : dt.toISOString().slice(0, 10));
            } else {
                $el.val(value);
            }
        });
    }

    function applySelectValues(data) {
        if (!data) return;

        $form.find('select.lookup-select').each(function () {
            const name = $(this).attr('name');
            const val = getDataValue(data, name);
            if (val != null && val !== '') {
                $(this).val(String(val)).trigger('change.select2');
            }
        });
    }

    function setData(data) {
        applyScalarFieldValues(data);
        applySelectValues(data);
    }

    function setDataWhenReady(data) {
        applyScalarFieldValues(data);

        if (typeof MetaForgeLookups === 'undefined') {
            applySelectValues(data);
            return $.when();
        }

        return MetaForgeLookups.initFormLookups($form, getFields(), data || {});
    }

    function load(id) {
        recordId = id;
        const entity = formDef.EntityName ?? formDef.entityName ?? $form.data('entity');
        return $.getJSON(`/api/metaforge/crud/${entity}/${id}`).then(data => setDataWhenReady(data));
    }

    function save() {
        const data = getData();
        const entity = formDef.EntityName ?? formDef.entityName ?? $form.data('entity');
        const method = recordId ? 'PUT' : 'POST';
        const url = recordId ? `/api/metaforge/crud/${entity}/${recordId}` : `/api/metaforge/crud/${entity}`;

        return $.ajax({ url, method, contentType: 'application/json', data: JSON.stringify(data) })
            .then(result => {
                if (!recordId && (result.Id ?? result.id)) recordId = result.Id ?? result.id;
                if (typeof DynamicGrid !== 'undefined') DynamicGrid.reload();
                return result;
            });
    }

    function showNew() {
        recordId = null;
        $form[0].reset();
        if (typeof MetaForgeLookups !== 'undefined') {
            MetaForgeLookups.initFormLookups($form, getFields());
        }
    }

    function reset() {
        recordId = null;
        $form[0].reset();
    }

    function refreshLookups(data) {
        if (typeof MetaForgeLookups !== 'undefined') {
            return MetaForgeLookups.initFormLookups($form, getFields(), data || {});
        }
        return $.when();
    }

    return { init, load, save, showNew, reset, getData, setData, setDataWhenReady, refreshLookups };
})();
