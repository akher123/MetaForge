/**
 * Shared detail grid row view/edit mode helpers for master-detail engines.
 */
const MetaForgeDetailRows = (function () {
    function createEditState() {
        return { editing: new Set(), snapshots: {} };
    }

    function isNewRow(row) {
        const id = row?.Id ?? row?.id;
        return id == null || id === '' || parseInt(id, 10) <= 0;
    }

    function isEditing(state, index) {
        return state.editing.has(index);
    }

    function isRowEmpty(row, fields) {
        if (!row) return true;
        const skip = new Set(['Id', 'id', '__display']);
        return !(fields || []).some(field => {
            const name = field.PropertyName ?? field.propertyName;
            if (skip.has(name)) return false;
            const val = row[name];
            return val != null && val !== '';
        });
    }

    function enterEdit(state, rows, index) {
        if (index == null || !rows[index]) return;
        state.snapshots[index] = JSON.parse(JSON.stringify(rows[index]));
        state.editing.add(index);
    }

    function finishEdit(state, index) {
        state.editing.delete(index);
        delete state.snapshots[index];
    }

    function cancelEdit(state, rows, index, fields) {
        if (index == null || !rows[index]) return 'noop';

        if (isNewRow(rows[index]) && isRowEmpty(rows[index], fields)) {
            rows.splice(index, 1);
            clearEditState(state);
            return 'removed';
        }

        if (state.snapshots[index]) {
            rows[index] = state.snapshots[index];
        }

        finishEdit(state, index);
        return 'restored';
    }

    function clearEditState(state) {
        state.editing.clear();
        state.snapshots = {};
    }

    function getLookupEntity(field) {
        const name = field.PropertyName ?? field.propertyName ?? '';
        return field.LookupEntity ?? field.lookupEntity
            ?? (name.endsWith('Id') ? name.replace(/Id$/, '') : '');
    }

    function getDisplayValue(row, field, rawValue) {
        const name = field.PropertyName ?? field.propertyName;
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        const val = rawValue ?? row[name];

        if (val == null || val === '') return '—';

        if (controlType === 'Checkbox') {
            return val === true || val === 'true' || val === 1 || val === '1' ? 'Yes' : 'No';
        }

        if (controlType === 'Dropdown' || controlType === 'Autocomplete') {
            return row.__display?.[name] ?? String(val);
        }

        if (controlType === 'Date' || controlType === 'DateTime') {
            const dt = new Date(val);
            if (!isNaN(dt.getTime())) {
                if (typeof MetaForgeLocale !== 'undefined') {
                    return controlType === 'Date'
                        ? MetaForgeLocale.formatDate(val)
                        : MetaForgeLocale.formatDateTime(val);
                }
                return controlType === 'Date'
                    ? dt.toLocaleDateString()
                    : dt.toLocaleString();
            }
        }

        if (controlType === 'Number') {
            const num = parseFloat(val);
            if (Number.isNaN(num)) return String(val);
            if (typeof MetaForgeLocale !== 'undefined') {
                return MetaForgeLocale.formatNumber(num, { maximumFractionDigits: 4 });
            }
            return num.toLocaleString(undefined, { maximumFractionDigits: 4 });
        }

        if (MetaForgeControlTypes.isRichText(controlType) && typeof MetaForgeGridDisplayFormat !== 'undefined') {
            const text = MetaForgeGridDisplayFormat.stripHtml(val);
            return text || '—';
        }

        return String(val);
    }

    function buildDisplayCell(field, row) {
        const name = field.PropertyName ?? field.propertyName;
        const text = getDisplayValue(row, field, row[name]);
        return `<span class="detail-view-cell" data-field="${escapeAttr(name)}">${escapeHtml(text)}</span>`;
    }

    function buildInlineControl(field, index, value, options) {
        const opts = options || {};
        const name = field.PropertyName ?? field.propertyName;
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);
        const lookupEntity = getLookupEntity(field);
        const readOnly = opts.readOnly === true;
        const required = (field.IsRequired ?? field.isRequired) ? 'required' : '';
        const disabled = readOnly ? 'disabled' : '';
        const readonly = readOnly ? 'readonly' : '';
        const val = value ?? '';
        const dataAttrs = buildDataAttrs(opts.dataAttrs || {}, index, name);
        const cascadeAttrs = typeof MetaForgeLookups !== 'undefined' ? MetaForgeLookups.cascadeAttrs(field) : '';
        let controlHtml;

        switch (controlType) {
            case 'TextArea':
                controlHtml = `<textarea class="form-control form-control-sm detail-input admin-form-control" ${dataAttrs} rows="2" ${readonly} ${required}>${escapeHtml(val)}</textarea>`;
                break;
            case MetaForgeControlTypes.RichText:
                controlHtml = `<div class="mf-rich-text mf-rich-text--compact" data-rich-text="${escapeAttr(name)}">
                    <input type="hidden" class="detail-input" ${dataAttrs} value="${escapeAttr(val)}" ${required} />
                    <div class="mf-rich-text-editor"></div>
                </div>`;
                break;
            case 'Number':
                controlHtml = `<input type="number" step="any" class="form-control form-control-sm detail-input admin-form-control detail-number" ${dataAttrs} value="${escapeAttr(val)}" ${readonly} ${required} />`;
                break;
            case 'Date':
                controlHtml = typeof MetaForgeDateInput !== 'undefined'
                    ? MetaForgeDateInput.buildDateFieldHtml({
                        value: val,
                        readonly,
                        required,
                        small: true,
                        detailInput: true,
                        extraAttrs: dataAttrs
                    })
                    : `<input type="date" class="form-control form-control-sm detail-input admin-form-control mf-date-input" ${dataAttrs} value="${escapeAttr(formatDateValue(val))}" ${readonly} ${required} />`;
                break;
            case 'DateTime':
                controlHtml = typeof MetaForgeDateInput !== 'undefined'
                    ? MetaForgeDateInput.buildDateTimeFieldHtml({
                        value: val,
                        readonly,
                        required,
                        small: true,
                        detailInput: true,
                        extraAttrs: dataAttrs
                    })
                    : `<input type="datetime-local" class="form-control form-control-sm detail-input admin-form-control mf-datetime-input" ${dataAttrs} value="${escapeAttr(formatDateTimeValue(val))}" ${readonly} ${required} />`;
                break;
            case 'Checkbox':
                controlHtml = `<div class="form-check"><input type="checkbox" class="form-check-input detail-input" ${dataAttrs} ${val ? 'checked' : ''} ${disabled} /></div>`;
                break;
            case 'Dropdown':
                controlHtml = `<select class="form-select form-select-sm lookup-select detail-input admin-form-control" ${dataAttrs} data-lookup="${lookupEntity}" ${cascadeAttrs} ${disabled} ${required}></select>`;
                break;
            case 'Autocomplete':
                controlHtml = `<select class="form-select form-select-sm lookup-autocomplete detail-input admin-form-control" ${dataAttrs} data-lookup="${lookupEntity}" ${cascadeAttrs} ${disabled} ${required}></select>`;
                break;
            case 'Hidden':
                return `<input type="hidden" class="detail-input" ${dataAttrs} value="${escapeAttr(val)}" />`;
            default:
                controlHtml = `<input type="text" class="form-control form-control-sm detail-input admin-form-control" ${dataAttrs} value="${escapeAttr(val)}" ${readonly} ${required} />`;
        }

        return wrapInlineControlHtml(controlHtml, name, index);
    }

    function wrapInlineControlHtml(controlHtml, fieldName, index) {
        return `
            <div class="detail-field-wrap" data-field-wrap="${escapeAttr(fieldName)}" data-index="${index}">
                ${controlHtml}
                <div class="invalid-feedback d-block" data-field-error="${escapeAttr(fieldName)}"></div>
            </div>`;
    }

    function clearRowFieldErrors($container) {
        const $root = $($container);
        $root.find('.detail-input.is-invalid').removeClass('is-invalid');
        $root.find('.detail-field-wrap.is-invalid').removeClass('is-invalid');
        $root.find('[data-field-error]').text('').hide();
        $root.find('.select2-selection.is-invalid').removeClass('is-invalid');
    }

    function showRowFieldError($container, index, fieldName, message) {
        const $root = $($container);
        const $wrap = $root.find(`.detail-field-wrap[data-field-wrap="${fieldName}"][data-index="${index}"]`).first();
        const $input = $wrap.find('.detail-input, .mf-date-text').first();

        if ($input.length) {
            $input.addClass('is-invalid');
            $input[0]?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
            $input.trigger('focus');
        }

        $wrap.addClass('is-invalid');
        $wrap.find(`[data-field-error="${fieldName}"]`).text(message).show();

        if ($input.hasClass('lookup-select') || $input.hasClass('lookup-autocomplete')) {
            $input.next('.select2-container').find('.select2-selection').addClass('is-invalid');
        }
    }

    function buildDataAttrs(extra, index, fieldName) {
        const attrs = { ...extra, 'data-field': fieldName, 'data-index': index };
        return Object.entries(attrs)
            .filter(([, v]) => v != null && v !== '')
            .map(([k, v]) => `${k}="${escapeAttr(v)}"`)
            .join(' ');
    }

    function buildActionCell(row, index, options) {
        const opts = options || {};
        const canEdit = opts.canEdit === true;
        const canDelete = opts.canDelete === true;
        const editing = opts.editing === true;
        const extraAttrs = opts.actionDataAttrs || '';
        const btns = [];

        if (canEdit) {
            if (editing) {
                btns.push(`<button type="button" class="btn btn-sm btn-outline-success btn-icon btn-detail-row-done" ${extraAttrs} data-index="${index}" title="Done editing" aria-label="Done editing">${MetaForgeIcons.apply}</button>`);
                btns.push(`<button type="button" class="btn btn-sm btn-outline-secondary btn-icon btn-detail-row-cancel" ${extraAttrs} data-index="${index}" title="Cancel editing" aria-label="Cancel editing">${MetaForgeIcons.cancel}</button>`);
            } else {
                btns.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-detail-row-edit" ${extraAttrs} data-index="${index}" title="Edit line" aria-label="Edit line">${MetaForgeIcons.edit}</button>`);
            }
        }

        if (canDelete) {
            btns.push(`<button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-detail" ${extraAttrs} data-index="${index}" title="Remove line" aria-label="Remove line">${MetaForgeIcons.removeLine}</button>`);
        }

        if (btns.length === 0) return '';

        return `<td class="text-center detail-actions-cell"><div class="btn-group btn-group-sm detail-row-actions">${btns.join('')}</div></td>`;
    }

    function syncDisplayFromInput(row, field, $input) {
        const name = field.PropertyName ?? field.propertyName;
        const controlType = MetaForgeControlTypes.normalize(field.ControlType ?? field.controlType);

        if (controlType === 'Dropdown' || controlType === 'Autocomplete') {
            row.__display = row.__display || {};
            const text = $input.find('option:selected').text()?.trim();
            if (text && text !== '-- Select --' && text !== '-- Search --') {
                row.__display[name] = text;
            } else {
                const selected = $input.val();
                if (selected && typeof MetaForgeLookups !== 'undefined') {
                    const entity = getLookupEntity(field);
                    $.getJSON(MetaForgeLookups.buildItemUrl(entity, selected), item => {
                        if (item) {
                            row.__display[name] = item.Text ?? item.text ?? String(selected);
                        }
                    });
                }
            }
            return;
        }

        if (controlType === 'Date' || controlType === 'DateTime') {
            row.__display = row.__display || {};
            const raw = $input.val();
            if (raw) {
                row.__display[name] = typeof MetaForgeDateInput !== 'undefined'
                    ? MetaForgeDateInput.formatDisplayValue(controlType, raw)
                    : (typeof MetaForgeLocale !== 'undefined'
                        ? (controlType === 'Date' ? MetaForgeLocale.formatDate(raw) : MetaForgeLocale.formatDateTime(raw))
                        : raw);
            }
        }
    }

    function resolveDisplayLabels(rows, fields) {
        const lookupFields = (fields || []).filter(f => {
            const controlType = f.ControlType ?? f.controlType;
            return controlType === 'Dropdown' || controlType === 'Autocomplete';
        });
        if (lookupFields.length === 0 || rows.length === 0) {
            return $.when();
        }

        const itemLoads = [];
        lookupFields.forEach(field => {
            const entity = getLookupEntity(field);
            const name = field.PropertyName ?? field.propertyName;
            const values = [...new Set(rows.map(row => row[name]).filter(v => v != null && v !== ''))];
            values.forEach(value => {
                itemLoads.push(
                    $.ajax({
                        url: `/api/metaforge/lookups/${encodeURIComponent(entity)}/item/${encodeURIComponent(value)}`,
                        dataType: 'json',
                        cache: false
                    }).then(item => ({ entity, value: String(value), text: item?.Text ?? item?.text ?? String(value) }))
                        .catch(() => ({ entity, value: String(value), text: String(value) }))
                );
            });
        });

        return $.when.apply($, itemLoads.length ? itemLoads : [$.when()]).then(function () {
            const maps = {};
            const results = arguments.length === 1 ? [arguments[0]] : Array.from(arguments);
            results.forEach(result => {
                if (!result?.entity || result.value == null) return;
                maps[result.entity] = maps[result.entity] || {};
                maps[result.entity][result.value] = result.text;
            });

            rows.forEach(row => {
                row.__display = row.__display || {};
                lookupFields.forEach(field => {
                    const name = field.PropertyName ?? field.propertyName;
                    const entity = getLookupEntity(field);
                    const val = row[name];
                    if (val == null || val === '' || row.__display[name]) return;
                    row.__display[name] = maps[entity]?.[String(val)] ?? String(val);
                });
            });
        });
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/'/g, '&#39;');
    }

    function formatDateValue(value) {
        return typeof MetaForgeDateInput !== 'undefined'
            ? MetaForgeDateInput.toDateInputValue(value)
            : MetaForgeGridDisplayFormat.formatDateInputValue(value);
    }

    function formatDateTimeValue(value) {
        return typeof MetaForgeDateInput !== 'undefined'
            ? MetaForgeDateInput.toDateTimeLocalValue(value)
            : (function () {
                if (!value) return '';
                const dt = new Date(value);
                if (isNaN(dt.getTime())) return value;
                const pad = n => String(n).padStart(2, '0');
                return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
            })();
    }

    function initDateInputsInRow($row) {
        if (typeof MetaForgeDateInput !== 'undefined') {
            MetaForgeDateInput.initScope($row);
        }
    }

    return {
        createEditState,
        isNewRow,
        isEditing,
        isRowEmpty,
        enterEdit,
        finishEdit,
        cancelEdit,
        clearEditState,
        getDisplayValue,
        buildDisplayCell,
        buildInlineControl,
        buildActionCell,
        clearRowFieldErrors,
        showRowFieldError,
        syncDisplayFromInput,
        resolveDisplayLabels,
        escapeHtml,
        escapeAttr,
        formatDateValue,
        formatDateTimeValue,
        initDateInputsInRow
    };
})();
