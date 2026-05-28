/**
 * Dynamic Form Engine - renders forms from metadata.
 */
const DynamicForm = (function () {
    let $form, formDef, recordId = null;
    let activeDefinition = null;

    function getFields(definition) {
        const source = definition ?? activeDefinition ?? formDef;
        return source?.Fields ?? source?.fields ?? [];
    }

    function getField(name) {
        return getFields().find(f => (f.PropertyName ?? f.propertyName) === name);
    }

    function destroyLookups($scope) {
        if (typeof MetaForgeLookups !== 'undefined') {
            MetaForgeLookups.destroyFormLookups($scope);
        }
    }

    function init(selector, definition, options) {
        const opts = options || {};
        $form = $(selector);
        formDef = definition;
        activeDefinition = definition;
        recordId = null;
        if (opts.layoutClass) {
            $form.addClass(opts.layoutClass);
        }
        render();
        clearFieldErrors($form);
        bindFieldErrorClear($form);
        bindConditionalLogic($form);
        applyAllConditionalStates($form);
        if (opts.initLookups !== false && typeof MetaForgeLookups !== 'undefined') {
            return MetaForgeLookups.initFormLookups($form, getFields()).then(function () {
                applyAllConditionalStates($form);
            });
        }
        return $.when();
    }

    function renderPreview(selector, definition, options) {
        const opts = options || {};
        const $target = $(selector);
        const fields = getFields(definition);
        const previousDefinition = activeDefinition;

        activeDefinition = definition;
        destroyLookups($target);
        $target.empty();

        if (opts.layoutClass) {
            $target.addClass(opts.layoutClass);
        }

        appendFields($target, fields);
        bindConditionalLogic($target);
        applyAllConditionalStates($target);

        if (opts.initLookups === false || typeof MetaForgeLookups === 'undefined') {
            activeDefinition = previousDefinition;
            return $.when();
        }

        return MetaForgeLookups.initFormLookups($target, fields, opts.data || {}).then(function () {
            applyAllConditionalStates($target);
            activeDefinition = previousDefinition;
        });
    }

    function render() {
        destroyLookups($form);
        $form.empty();
        appendFields($form, getFields());
    }

    function appendFields($container, fields) {
        const sections = groupBySection(fields);

        Object.keys(sections).forEach(sectionName => {
            if (sectionName !== 'default') {
                $container.append(`<div class="admin-form-section-title">${sectionName}</div>`);
            }

            sections[sectionName].forEach(field => {
                const name = field.PropertyName ?? field.propertyName;
                const controlType = field.ControlType ?? field.controlType ?? 'TextBox';
                const isRequired = field.IsRequired ?? field.isRequired;
                const label = field.Label ?? field.label;

                if (controlType === 'Hidden') {
                    $container.append(buildControl(field));
                    return;
                }

                const col = $(`<div class="admin-form-field" data-field-container="${name}"></div>`);
                col.append(`<label class="admin-form-label" data-field-label="${name}">${label}${isRequired ? ' <span class="required-mark">*</span>' : ''}</label>`);
                col.append(buildControl(field));
                $container.append(col);
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
        let controlHtml;

        switch (controlType) {
            case 'TextArea':
                controlHtml = `<textarea class="form-control admin-form-control" name="${name}" ${readonly} ${required} rows="3"></textarea>`;
                break;
            case 'Number':
                controlHtml = `<input type="number" step="any" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'Date':
                controlHtml = `<input type="date" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'DateTime':
                controlHtml = `<input type="datetime-local" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
                break;
            case 'Checkbox':
                controlHtml = `<div class="form-check mt-1"><input type="checkbox" class="form-check-input" name="${name}" ${readonly} ${disabled} /></div>`;
                break;
            case 'Dropdown':
                controlHtml = `<select class="form-select admin-form-control lookup-select" name="${name}" data-lookup="${lookupEntity || (name || '').replace(/Id$/, '')}" ${cascadeAttrs} ${required} ${disabled}></select>`;
                break;
            case 'Autocomplete':
                controlHtml = `<select class="form-select admin-form-control lookup-autocomplete" name="${name}" data-lookup="${lookupEntity || (name || '').replace(/Id$/, '')}" ${cascadeAttrs} ${required} ${disabled}></select>`;
                break;
            case 'Hidden':
                return `<input type="hidden" name="${name}" />`;
            default:
                controlHtml = `<input type="text" class="form-control admin-form-control" name="${name}" ${readonly} ${required} />`;
        }

        return wrapControlHtml(controlHtml, name);
    }

    function wrapControlHtml(controlHtml, name) {
        return `
            <div class="field-control-wrap" data-field-wrap="${name}">
                ${controlHtml}
                <div class="invalid-feedback" data-field-error="${name}"></div>
            </div>`;
    }

    function findFieldContainer($scope, fieldName) {
        const $byData = $scope.find(`[data-field-container="${fieldName}"]`).first();
        if ($byData.length) return $byData;

        const $wrap = findFieldWrap($scope, fieldName);
        return $wrap.closest('.admin-form-field');
    }

    function collectFormData($scope) {
        const data = {};
        $scope.find('[name]').each(function () {
            const $el = $(this);
            const name = $el.attr('name');
            data[name] = readFieldValue($el, getField(name));
        });
        return data;
    }

    function applyFieldConditionalState($scope, field) {
        if (typeof FieldConditionalEngine === 'undefined') return;

        const name = field.PropertyName ?? field.propertyName;
        const controlType = field.ControlType ?? field.controlType ?? 'TextBox';
        if (controlType === 'Hidden') return;

        const data = collectFormData($scope);
        const state = FieldConditionalEngine.evaluateEffectiveState(field, data);
        const $container = findFieldContainer($scope, name);
        const $input = findFieldInput($scope, name);
        const $label = $scope.find(`[data-field-label="${name}"]`).first();

        if ($container.length) {
            $container.toggleClass('d-none', !state.visible);
        }

        if ($input.length) {
            $input.prop('required', state.required && state.visible);

            if ($input.is('select')) {
                $input.prop('disabled', false);
                const $s2 = $input.next('.select2-container');
                if (state.readOnly) {
                    $input.attr('tabindex', '-1');
                    $s2.addClass('pe-none field-readonly-select');
                } else {
                    $input.removeAttr('tabindex');
                    $s2.removeClass('pe-none field-readonly-select');
                }
            } else if ($input.hasClass('form-check-input')) {
                $input.prop('disabled', state.readOnly);
            } else {
                $input.prop('readonly', state.readOnly);
                $input.prop('disabled', false);
            }
        }

        if ($label.length) {
            const label = field.Label ?? field.label ?? name;
            $label.html(`${label}${state.required && state.visible ? ' <span class="required-mark">*</span>' : ''}`);
        }
    }

    function applyAllConditionalStates($scope) {
        if (typeof FieldConditionalEngine === 'undefined') return;

        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        getFields().forEach(function (field) {
            applyFieldConditionalState($root, field);
        });
    }

    function bindConditionalLogic($scope) {
        const $root = $scope ? $($scope) : $form;
        $root.off('.conditionalLogic');
        $root.on('input.conditionalLogic change.conditionalLogic', '.form-control, .form-select, .form-check-input', function () {
            applyAllConditionalStates($root);
        });
    }

    function findFieldWrap($scope, fieldName) {
        return $scope.find(`[data-field-wrap="${fieldName}"]`).first();
    }

    function findFieldInput($scope, fieldName) {
        const $wrap = findFieldWrap($scope, fieldName);
        if ($wrap.length) {
            const $input = $wrap.find('.form-control, .form-select, .form-check-input').first();
            if ($input.length) return $input;
        }

        return $scope.find(`[name="${fieldName}"]`).first();
    }

    function clearFieldErrors($scope) {
        const $root = $scope ? $($scope) : $form;
        if (!$root.length) return;

        $root.find('.is-invalid').removeClass('is-invalid');
        $root.find('.field-control-wrap.is-invalid').removeClass('is-invalid');
        $root.find('[data-field-error]').text('').hide();
        $root.find('.detail-field-wrap .is-invalid').removeClass('is-invalid');
    }

    function showFieldError($scope, fieldName, message) {
        const $root = $scope ? $($scope) : $form;
        const $wrap = findFieldWrap($root, fieldName);
        const $input = findFieldInput($root, fieldName);
        const $feedback = $wrap.find(`[data-field-error="${fieldName}"]`);

        if ($input.length) {
            $input.addClass('is-invalid');
        }
        if ($wrap.length) {
            $wrap.addClass('is-invalid');
        }
        if ($feedback.length) {
            $feedback.text(message).show();
        }

        if ($input.hasClass('lookup-select') || $input.hasClass('lookup-autocomplete')) {
            $input.next('.select2-container').find('.select2-selection').addClass('is-invalid');
        }
    }

    function showFieldErrors($scope, fieldErrors) {
        clearFieldErrors($scope);
        if (!fieldErrors) return false;

        let count = 0;
        Object.keys(fieldErrors).forEach(function (fieldName) {
            const message = fieldErrors[fieldName];
            const text = Array.isArray(message) ? message[0] : message;
            if (!text) return;
            showFieldError($scope, fieldName, text);
            count++;
        });

        if (count > 0) {
            const $first = ($scope ? $($scope) : $form).find('.is-invalid').first();
            if ($first.length) {
                $first.trigger('focus');
                $first[0]?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
            }
        }

        return count > 0;
    }

    function parseAjaxFieldErrors(xhr) {
        const json = xhr?.responseJSON;
        const fields = {};

        if (json?.fieldErrors && typeof json.fieldErrors === 'object') {
            Object.keys(json.fieldErrors).forEach(function (key) {
                const value = json.fieldErrors[key];
                fields[key] = Array.isArray(value) ? value[0] : value;
            });
        }

        return {
            general: json?.error ?? json?.title ?? xhr?.statusText ?? 'Save failed.',
            fields: fields
        };
    }

    function handleAjaxValidationError($scope, xhr) {
        const parsed = parseAjaxFieldErrors(xhr);
        if (Object.keys(parsed.fields).length > 0) {
            showFieldErrors($scope, parsed.fields);
            return true;
        }

        return false;
    }

    function validateRequiredFields($scope, fields, data) {
        clearFieldErrors($scope);
        const errors = {};
        const formData = data || collectFormData($scope ? $($scope) : $form);

        (fields || []).forEach(function (field) {
            const name = field.PropertyName ?? field.propertyName;
            const label = field.Label ?? field.label ?? name;
            const controlType = field.ControlType ?? field.controlType ?? 'TextBox';

            let isRequired = field.IsRequired ?? field.isRequired;
            let isVisible = field.IsVisible ?? field.isVisible ?? true;

            if (typeof FieldConditionalEngine !== 'undefined') {
                const state = FieldConditionalEngine.evaluateEffectiveState(field, formData);
                isRequired = state.required;
                isVisible = state.visible;
            }

            if (!isRequired || !isVisible || controlType === 'Hidden') return;

            const val = formData[name];
            const isLookup = controlType === 'Dropdown' || controlType === 'Autocomplete'
                || (name.endsWith('Id') && name !== 'Id');

            if (isLookup) {
                const num = parseInt(val, 10);
                if (!num || num <= 0) errors[name] = `${label} is required.`;
            } else if (controlType === 'Checkbox') {
                if (val !== true && val !== 'true' && val !== 1 && val !== '1') {
                    errors[name] = `${label} is required.`;
                }
            } else if (val == null || String(val).trim() === '') {
                errors[name] = `${label} is required.`;
            }
        });

        return showFieldErrors($scope, errors) ? errors : null;
    }

    function bindFieldErrorClear($scope) {
        const $root = $scope ? $($scope) : $form;
        $root.off('.fieldValidationClear');
        $root.on('input.fieldValidationClear change.fieldValidationClear', '.form-control, .form-select, .form-check-input', function () {
            const name = $(this).attr('name');
            if (!name) return;

            const $wrap = findFieldWrap($root, name);
            $(this).removeClass('is-invalid');
            $wrap.removeClass('is-invalid');
            $wrap.find(`[data-field-error="${name}"]`).text('').hide();
            $(this).next('.select2-container').find('.select2-selection').removeClass('is-invalid');
        });
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

        if ((controlType === 'Dropdown' || controlType === 'Autocomplete') && (field?.PropertyName ?? field?.propertyName ?? '').endsWith('Id')) {
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

        $form.find('select.lookup-select, select.lookup-autocomplete').each(function () {
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
            applyAllConditionalStates($form);
            return $.when();
        }

        return MetaForgeLookups.initFormLookups($form, getFields(), data || {}).then(function () {
            applyAllConditionalStates($form);
        });
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

        clearFieldErrors($form);

        return $.ajax({ url, method, contentType: 'application/json', data: JSON.stringify(data) })
            .then(result => {
                if (!recordId && (result.Id ?? result.id)) recordId = result.Id ?? result.id;
                if (typeof DynamicGrid !== 'undefined') DynamicGrid.reload();
                return result;
            })
            .fail(function (xhr) {
                handleAjaxValidationError($form, xhr);
            });
    }

    function showNew() {
        recordId = null;
        clearFieldErrors($form);
        if ($form[0]?.reset) {
            $form[0].reset();
        }
        applyAllConditionalStates($form);
        if (typeof MetaForgeLookups !== 'undefined') {
            return MetaForgeLookups.initFormLookups($form, getFields()).then(function () {
                applyAllConditionalStates($form);
            });
        }
        return $.when();
    }

    function reset() {
        recordId = null;
        clearFieldErrors($form);
        if ($form[0]?.reset) {
            $form[0].reset();
        }
        destroyLookups($form);
    }

    function refreshLookups(data) {
        if (typeof MetaForgeLookups !== 'undefined') {
            return MetaForgeLookups.initFormLookups($form, getFields(), data || {});
        }
        return $.when();
    }

    return {
        init,
        renderPreview,
        load,
        save,
        showNew,
        reset,
        getData,
        setData,
        setDataWhenReady,
        refreshLookups,
        clearFieldErrors,
        showFieldError,
        showFieldErrors,
        handleAjaxValidationError,
        validateRequiredFields,
        applyAllConditionalStates,
        parseAjaxFieldErrors
    };
})();
