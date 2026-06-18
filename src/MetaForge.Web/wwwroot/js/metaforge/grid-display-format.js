/**
 * Shared date/date-time formatting for list grids (preset keys match GridDisplayFormats on the server).
 */
const MetaForgeGridDisplayFormat = (function () {
    const ControlTypeDate = 'Date';
    const ControlTypeDateTime = 'DateTime';

    const PRESETS = [
        { key: '', label: 'Default (from field type)', group: 'General' },
        { key: 'date-short', label: 'dd/MM/yyyy', group: 'Date' },
        { key: 'date-iso', label: 'yyyy-MM-dd', group: 'Date' },
        { key: 'date-long', label: 'dd MMM yyyy', group: 'Date' },
        { key: 'locale-date', label: 'Locale date', group: 'Date' },
        { key: 'datetime-short', label: 'dd/MM/yyyy HH:mm', group: 'Date & time' },
        { key: 'datetime-full', label: 'dd/MM/yyyy HH:mm:ss', group: 'Date & time' },
        { key: 'datetime-iso', label: 'yyyy-MM-dd HH:mm', group: 'Date & time' },
        { key: 'locale-datetime', label: 'Locale date & time', group: 'Date & time' }
    ];

    function getDefaultForControlType(controlType) {
        const ct = (controlType || '').toString();
        if (ct === ControlTypeDate) return 'date-short';
        if (ct === ControlTypeDateTime) return 'datetime-short';
        return '';
    }

    function isTemporalControlType(controlType) {
        const ct = (controlType || '').toString();
        return ct === ControlTypeDate || ct === ControlTypeDateTime;
    }

    function resolveFormatKey(displayFormat, controlType) {
        const fmt = (displayFormat || '').toString().trim();
        if (fmt) return fmt;
        return getDefaultForControlType(controlType);
    }

    function parseDate(value) {
        if (value == null || value === '') return null;
        if (value instanceof Date) return isNaN(value.getTime()) ? null : value;
        const dt = new Date(value);
        return isNaN(dt.getTime()) ? null : dt;
    }

    function pad2(n) {
        return String(n).padStart(2, '0');
    }

    function formatWithTokens(dt, pattern) {
        const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        const map = {
            yyyy: String(dt.getFullYear()),
            yy: String(dt.getFullYear()).slice(-2),
            MMM: months[dt.getMonth()],
            MM: pad2(dt.getMonth() + 1),
            dd: pad2(dt.getDate()),
            HH: pad2(dt.getHours()),
            mm: pad2(dt.getMinutes()),
            ss: pad2(dt.getSeconds())
        };
        return pattern.replace(/yyyy|yy|MMM|MM|dd|HH|mm|ss/g, token => map[token] ?? token);
    }

    function applyPreset(dt, formatKey) {
        switch (formatKey) {
            case 'date-short':
                return formatWithTokens(dt, 'dd/MM/yyyy');
            case 'date-iso':
                return formatWithTokens(dt, 'yyyy-MM-dd');
            case 'date-long':
                return formatWithTokens(dt, 'dd MMM yyyy');
            case 'datetime-short':
                return formatWithTokens(dt, 'dd/MM/yyyy HH:mm');
            case 'datetime-full':
                return formatWithTokens(dt, 'dd/MM/yyyy HH:mm:ss');
            case 'datetime-iso':
                return formatWithTokens(dt, 'yyyy-MM-dd HH:mm');
            case 'locale-date':
                return dt.toLocaleDateString();
            case 'locale-datetime':
                return dt.toLocaleString();
            default:
                if (/[ydHm]/.test(formatKey)) {
                    return formatWithTokens(dt, formatKey);
                }
                return dt.toLocaleString();
        }
    }

    function formatValue(value, displayFormat, controlType) {
        if (value == null || value === '') return '';
        const dt = parseDate(value);
        if (!dt) return String(value);

        const formatKey = resolveFormatKey(displayFormat, controlType);
        if (!formatKey && !isTemporalControlType(controlType)) return String(value);

        return applyPreset(dt, formatKey || getDefaultForControlType(controlType));
    }

    /** yyyy-MM-dd for input type="date" — local calendar date, not UTC (avoids off-by-one day). */
    function formatDateInputValue(value) {
        if (value == null || value === '') return '';
        if (value instanceof Date) {
            return isNaN(value.getTime()) ? '' : formatWithTokens(value, 'yyyy-MM-dd');
        }
        const s = String(value).trim();
        const dateOnly = s.match(/^(\d{4}-\d{2}-\d{2})/);
        if (dateOnly && (s.length === 10 || s[10] === 'T' || s[10] === ' ')) {
            return dateOnly[1];
        }
        const dt = parseDate(value);
        return dt ? formatWithTokens(dt, 'yyyy-MM-dd') : s;
    }

    function stripHtml(value) {
        if (value == null || value === '') return '';
        const el = document.createElement('div');
        el.innerHTML = String(value);
        return (el.textContent || el.innerText || '').trim();
    }

    function buildSelectOptions(selected) {
        const sel = (selected || '').toString();
        return PRESETS.map(p =>
            `<option value="${p.key}" ${sel === p.key ? 'selected' : ''}>${p.label}</option>`
        ).join('');
    }

    return {
        PRESETS,
        getDefaultForControlType,
        isTemporalControlType,
        resolveFormatKey,
        formatValue,
        formatDateInputValue,
        buildSelectOptions,
        stripHtml
    };
})();
