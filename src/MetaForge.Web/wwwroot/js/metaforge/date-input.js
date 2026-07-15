/**
 * Preference-aware date/datetime inputs for forms and detail grids.
 * Display follows user preference first, then system (via __METAFORGE_LOCALE__).
 */
const MetaForgeDateInput = (function () {
    const boundFields = new WeakSet();

    function getCulture() {
        return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.culture) || undefined;
    }

    function getFormatKey(isDateTime) {
        if (typeof MetaForgeGridDisplayFormat !== 'undefined'
            && typeof MetaForgeGridDisplayFormat.getEffectiveFormatKey === 'function') {
            return MetaForgeGridDisplayFormat.getEffectiveFormatKey(isDateTime);
        }
        const locale = window.__METAFORGE_LOCALE__ || {};
        return isDateTime
            ? (locale.dateTimeFormat || 'locale-datetime')
            : (locale.dateFormat || 'locale-date');
    }

    function formatDisplayValue(controlType, value) {
        if (value == null || value === '') return '';

        const ct = (controlType || '').toString();
        if (ct === 'DateTime') {
            if (typeof MetaForgeLocale !== 'undefined' && typeof MetaForgeLocale.formatDateTime === 'function') {
                return MetaForgeLocale.formatDateTime(value);
            }
        } else if (ct === 'Date') {
            if (typeof MetaForgeLocale !== 'undefined' && typeof MetaForgeLocale.formatDate === 'function') {
                return MetaForgeLocale.formatDate(value);
            }
        }

        return String(value);
    }

    function toIsoDateValue(value) {
        if (typeof MetaForgeGridDisplayFormat !== 'undefined') {
            return MetaForgeGridDisplayFormat.formatDateInputValue(value);
        }
        if (value == null || value === '') return '';
        const dt = new Date(value);
        if (isNaN(dt.getTime())) return String(value);
        const pad = n => String(n).padStart(2, '0');
        return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}`;
    }

    function toDateInputValue(value) {
        return toIsoDateValue(value);
    }

    function toDateTimeLocalValue(value) {
        if (typeof MetaForgeGridDisplayFormat !== 'undefined'
            && typeof MetaForgeGridDisplayFormat.formatDateTimeInputValue === 'function') {
            return MetaForgeGridDisplayFormat.formatDateTimeInputValue(value);
        }
        if (!value) return '';
        const dt = new Date(value);
        if (isNaN(dt.getTime())) return String(value);
        const pad = n => String(n).padStart(2, '0');
        return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
    }

    function getPlaceholder(isDateTime) {
        const sample = isDateTime ? new Date(2026, 6, 15, 14, 30, 0) : new Date(2026, 6, 15);
        return formatDisplayValue(isDateTime ? 'DateTime' : 'Date', sample);
    }

    function escAttr(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;');
    }

    function buildDateFieldHtml(options) {
        const opts = options || {};
        const nameAttr = opts.name ? ` name="${escAttr(opts.name)}"` : '';
        const extra = opts.extraAttrs ? ` ${opts.extraAttrs}` : '';
        const readonly = opts.readonly ? ' readonly' : '';
        const required = opts.required ? ' required' : '';
        const disabled = opts.readonly ? ' disabled' : '';
        const sm = opts.small ? ' input-group-sm' : '';
        const controlSm = opts.small ? ' form-control-sm' : '';
        const btnSm = opts.small ? ' btn-sm' : '';
        const detailClass = opts.detailInput ? 'detail-input ' : '';
        const isoValue = toIsoDateValue(opts.value);
        const placeholder = getPlaceholder(false);

        return `<div class="mf-date-field input-group${sm}" data-temporal="Date">
            <input type="hidden"${nameAttr} class="${detailClass}mf-date-iso admin-form-control" value="${escAttr(isoValue)}"${required}${extra} />
            <input type="text" class="form-control${controlSm} mf-date-text admin-form-control" autocomplete="off" placeholder="${escAttr(placeholder)}" title="Format: ${escAttr(placeholder)}"${readonly}${required} />
            <button type="button" class="btn btn-outline-secondary${btnSm} mf-date-open" title="Open calendar"${disabled}><i class="fa-regular fa-calendar" aria-hidden="true"></i></button>
            <input type="date" class="mf-date-native" tabindex="-1" aria-hidden="true" />
        </div>`;
    }

    function buildDateTimeFieldHtml(options) {
        const opts = options || {};
        const nameAttr = opts.name ? ` name="${escAttr(opts.name)}"` : '';
        const extra = opts.extraAttrs ? ` ${opts.extraAttrs}` : '';
        const readonly = opts.readonly ? ' readonly' : '';
        const required = opts.required ? ' required' : '';
        const disabled = opts.readonly ? ' disabled' : '';
        const sm = opts.small ? ' input-group-sm' : '';
        const controlSm = opts.small ? ' form-control-sm' : '';
        const btnSm = opts.small ? ' btn-sm' : '';
        const detailClass = opts.detailInput ? 'detail-input ' : '';
        const isoValue = toDateTimeLocalValue(opts.value);
        const placeholder = getPlaceholder(true);

        return `<div class="mf-date-field input-group${sm}" data-temporal="DateTime">
            <input type="hidden"${nameAttr} class="${detailClass}mf-date-iso admin-form-control" value="${escAttr(isoValue)}"${required}${extra} />
            <input type="text" class="form-control${controlSm} mf-date-text admin-form-control" autocomplete="off" placeholder="${escAttr(placeholder)}" title="Format: ${escAttr(placeholder)}"${readonly}${required} />
            <button type="button" class="btn btn-outline-secondary${btnSm} mf-date-open" title="Open calendar"${disabled}><i class="fa-regular fa-calendar" aria-hidden="true"></i></button>
            <input type="datetime-local" class="mf-date-native" tabindex="-1" aria-hidden="true" />
        </div>`;
    }

    function buildLangAttr() {
        const culture = getCulture();
        return culture ? ` lang="${culture}"` : '';
    }

    function buildDateAttrs(readonly, required) {
        return `${buildLangAttr()} class="form-control admin-form-control mf-date-input" ${readonly} ${required}`;
    }

    function buildDateTimeAttrs(readonly, required) {
        return `${buildLangAttr()} class="form-control admin-form-control mf-datetime-input" ${readonly} ${required}`;
    }

    function buildSmDateAttrs(readonly, required) {
        return `${buildLangAttr()} class="form-control form-control-sm detail-input admin-form-control mf-date-input" ${readonly} ${required}`;
    }

    function buildSmDateTimeAttrs(readonly, required) {
        return `${buildLangAttr()} class="form-control form-control-sm detail-input admin-form-control mf-datetime-input" ${readonly} ${required}`;
    }

    function parseTextToIso(text, isDateTime) {
        if (typeof MetaForgeGridDisplayFormat !== 'undefined'
            && typeof MetaForgeGridDisplayFormat.parseFormattedDate === 'function') {
            const dt = MetaForgeGridDisplayFormat.parseFormattedDate(
                text,
                getFormatKey(isDateTime),
                getCulture(),
                isDateTime);
            if (!dt || isNaN(dt.getTime())) return '';
            return isDateTime ? toDateTimeLocalValue(dt) : toIsoDateValue(dt);
        }

        const dt = new Date(text);
        if (isNaN(dt.getTime())) return '';
        return isDateTime ? toDateTimeLocalValue(dt) : toIsoDateValue(dt);
    }

    function syncDisplay(field) {
        const isDateTime = field.dataset.temporal === 'DateTime';
        const isoInput = field.querySelector('.mf-date-iso');
        const textInput = field.querySelector('.mf-date-text');
        const nativeInput = field.querySelector('.mf-date-native');
        if (!isoInput || !textInput) return;

        const isoValue = isoInput.value || '';
        textInput.value = isoValue ? formatDisplayValue(isDateTime ? 'DateTime' : 'Date', isoValue) : '';
        if (nativeInput) {
            nativeInput.value = isoValue;
        }
    }

    function syncIsoFromText(field) {
        const isDateTime = field.dataset.temporal === 'DateTime';
        const isoInput = field.querySelector('.mf-date-iso');
        const textInput = field.querySelector('.mf-date-text');
        const nativeInput = field.querySelector('.mf-date-native');
        if (!isoInput || !textInput) return;

        const text = (textInput.value || '').trim();
        if (!text) {
            isoInput.value = '';
            if (nativeInput) nativeInput.value = '';
            return;
        }

        const iso = parseTextToIso(text, isDateTime);
        isoInput.value = iso;
        if (nativeInput) nativeInput.value = iso;
        if (iso) {
            textInput.value = formatDisplayValue(isDateTime ? 'DateTime' : 'Date', iso);
        }
    }

    function bindField(field) {
        if (!field || boundFields.has(field)) return;
        boundFields.add(field);

        const textInput = field.querySelector('.mf-date-text');
        const nativeInput = field.querySelector('.mf-date-native');
        const openBtn = field.querySelector('.mf-date-open');
        const isoInput = field.querySelector('.mf-date-iso');

        if (textInput) {
            textInput.addEventListener('blur', function () {
                syncIsoFromText(field);
                isoInput?.dispatchEvent(new Event('change', { bubbles: true }));
            });
            textInput.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    textInput.blur();
                }
            });
        }

        if (nativeInput) {
            nativeInput.addEventListener('change', function () {
                if (!isoInput) return;
                isoInput.value = nativeInput.value || '';
                syncDisplay(field);
                isoInput.dispatchEvent(new Event('change', { bubbles: true }));
                isoInput.dispatchEvent(new Event('input', { bubbles: true }));
            });
        }

        if (openBtn && nativeInput) {
            openBtn.addEventListener('click', function () {
                if (openBtn.disabled) return;
                if (typeof nativeInput.showPicker === 'function') {
                    nativeInput.showPicker();
                } else {
                    nativeInput.focus();
                    nativeInput.click();
                }
            });
        }

        syncDisplay(field);
    }

    function setFieldValue(fieldOrScope, value, controlType) {
        const root = fieldOrScope && fieldOrScope.jquery ? fieldOrScope[0] : fieldOrScope;
        if (!root) return;

        const field = root.classList && root.classList.contains('mf-date-field')
            ? root
            : root.querySelector?.('.mf-date-field');

        if (!field) return;

        const isDateTime = (controlType || field.dataset.temporal || 'Date') === 'DateTime';
        const isoInput = field.querySelector('.mf-date-iso');
        if (!isoInput) return;

        isoInput.value = isDateTime ? toDateTimeLocalValue(value) : toIsoDateValue(value);
        syncDisplay(field);
    }

    function initScope($scope) {
        const root = $scope && $scope.length ? $scope[0] : document;
        if (!root || !root.querySelectorAll) return;

        root.querySelectorAll('.mf-date-field').forEach(bindField);

        root.querySelectorAll('input[type="date"]:not(.mf-date-native), input[type="datetime-local"]:not(.mf-date-native)').forEach(function (legacyInput) {
            if (legacyInput.closest('.mf-date-field')) return;

            const isDateTime = legacyInput.type === 'datetime-local';
            const wrapper = document.createElement('div');
            wrapper.innerHTML = isDateTime
                ? buildDateTimeFieldHtml({
                    name: legacyInput.getAttribute('name') || undefined,
                    value: legacyInput.value,
                    readonly: legacyInput.hasAttribute('readonly'),
                    required: legacyInput.hasAttribute('required'),
                    small: legacyInput.classList.contains('form-control-sm'),
                    detailInput: legacyInput.classList.contains('detail-input'),
                    extraAttrs: Array.from(legacyInput.attributes)
                        .filter(attr => attr.name.startsWith('data-'))
                        .map(attr => `${attr.name}="${escAttr(attr.value)}"`)
                        .join(' ')
                })
                : buildDateFieldHtml({
                    name: legacyInput.getAttribute('name') || undefined,
                    value: legacyInput.value,
                    readonly: legacyInput.hasAttribute('readonly'),
                    required: legacyInput.hasAttribute('required'),
                    small: legacyInput.classList.contains('form-control-sm'),
                    detailInput: legacyInput.classList.contains('detail-input'),
                    extraAttrs: Array.from(legacyInput.attributes)
                        .filter(attr => attr.name.startsWith('data-'))
                        .map(attr => `${attr.name}="${escAttr(attr.value)}"`)
                        .join(' ')
                });

            const field = wrapper.firstElementChild;
            if (!field) return;
            legacyInput.replaceWith(field);
            bindField(field);
        });
    }

    return {
        getCulture,
        formatDisplayValue,
        toDateInputValue,
        toDateTimeLocalValue,
        buildDateFieldHtml,
        buildDateTimeFieldHtml,
        buildLangAttr,
        buildDateAttrs,
        buildDateTimeAttrs,
        buildSmDateAttrs,
        buildSmDateTimeAttrs,
        setFieldValue,
        initScope
    };
})();
