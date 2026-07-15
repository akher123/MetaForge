/**
 * Shared date/date-time formatting for list grids (preset keys match GridDisplayFormats on the server).
 */
const MetaForgeGridDisplayFormat = (function () {
    const ControlTypeDate = 'Date';
    const ControlTypeDateTime = 'DateTime';
    const PatternPrefix = 'pattern:';

    const PRESETS = [
        { key: '', label: 'Default (from field type)', group: 'General' },
        { key: 'date-short', label: 'dd/MM/yyyy', group: 'Date' },
        { key: 'date-iso', label: 'yyyy-MM-dd', group: 'Date' },
        { key: 'date-long', label: 'dd MMM yyyy', group: 'Date' },
        { key: 'date-us', label: 'MM/dd/yyyy', group: 'Date' },
        { key: 'locale-date', label: 'Locale date', group: 'Date' },
        { key: 'locale-long', label: 'Locale long', group: 'Date' },
        { key: 'datetime-short', label: 'dd/MM/yyyy HH:mm', group: 'Date & time' },
        { key: 'datetime-full', label: 'dd/MM/yyyy HH:mm:ss', group: 'Date & time' },
        { key: 'datetime-iso', label: 'yyyy-MM-dd HH:mm', group: 'Date & time' },
        { key: 'locale-datetime', label: 'Locale date & time', group: 'Date & time' },
        { key: 'locale-long-datetime', label: 'Locale long date & time', group: 'Date & time' }
    ];

    function getLocaleConfig() {
        return window.__METAFORGE_LOCALE__ || {};
    }

    function getLocale() {
        return getLocaleConfig().culture || undefined;
    }

    function getEffectiveDateFormat(formatKey, controlType) {
        const key = normalizeFormatKey(formatKey, controlType || ControlTypeDate);
        if (!key || key === 'locale-date') {
            return getLocaleConfig().dateFormat || 'locale-date';
        }
        return key;
    }

    function getEffectiveDateTimeFormat(formatKey, controlType) {
        const key = normalizeFormatKey(formatKey, controlType || ControlTypeDateTime);
        if (!key || key === 'locale-datetime') {
            return getLocaleConfig().dateTimeFormat || 'locale-datetime';
        }
        return key;
    }

    function getDefaultForControlType(controlType) {
        const ct = (controlType || '').toString();
        if (ct === ControlTypeDate) return 'locale-date';
        if (ct === ControlTypeDateTime) return 'locale-datetime';
        return '';
    }

    function normalizeFormatKey(formatKey, controlType) {
        const key = (formatKey || '').toString().trim();
        if (!key) return getDefaultForControlType(controlType);
        if (key === 'd') return 'locale-date';
        if (key === 'D') return 'locale-long';
        if (key === 'g') return 'locale-datetime';
        if (key === 'G') return 'locale-long-datetime';
        return key;
    }

    function usesSystemPreference(formatKey, controlType) {
        const key = normalizeFormatKey(formatKey, controlType);
        return key === 'locale-date' || key === 'locale-datetime';
    }

    function isTemporalControlType(controlType) {
        const ct = (controlType || '').toString();
        return ct === ControlTypeDate || ct === ControlTypeDateTime;
    }

    function resolveFormatKey(displayFormat, controlType) {
        return normalizeFormatKey(displayFormat, controlType) || '';
    }

    function parseDate(value) {
        if (value == null || value === '') return null;
        if (value instanceof Date) return isNaN(value.getTime()) ? null : value;

        const s = String(value).trim();
        const dateOnly = s.match(/^(\d{4}-\d{2}-\d{2})/);
        if (dateOnly && (s.length === 10 || s[10] === 'T' || s[10] === ' ')) {
            const parts = dateOnly[1].split('-').map(Number);
            return new Date(parts[0], parts[1] - 1, parts[2]);
        }

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

    function formatLocaleShortDate(dt, culture) {
        try {
            return new Intl.DateTimeFormat(culture, { dateStyle: 'short' }).format(dt);
        } catch (_) {
            return dt.toLocaleDateString();
        }
    }

    function formatLocaleShortDateTime(dt, culture) {
        try {
            return new Intl.DateTimeFormat(culture, { dateStyle: 'short', timeStyle: 'short' }).format(dt);
        } catch (_) {
            return dt.toLocaleString();
        }
    }

    function formatWithPattern(dt, pattern, culture) {
        if (pattern === 'd') return formatLocaleShortDate(dt, culture);
        if (pattern === 'D') {
            try {
                return new Intl.DateTimeFormat(culture, { dateStyle: 'full' }).format(dt);
            } catch (_) {
                return dt.toLocaleDateString(culture);
            }
        }
        if (pattern === 'g') return formatLocaleShortDateTime(dt, culture);
        if (pattern === 'G') {
            try {
                return new Intl.DateTimeFormat(culture, { dateStyle: 'full', timeStyle: 'short' }).format(dt);
            } catch (_) {
                return dt.toLocaleString(culture);
            }
        }
        return formatWithTokens(dt, pattern);
    }

    function applyPreset(dt, formatKey, cultureOverride, controlType) {
        const culture = cultureOverride || getLocale();
        let key = normalizeFormatKey(formatKey, controlType);

        if (key === 'locale-date') {
            key = getEffectiveDateFormat(key, ControlTypeDate);
            if (key === 'locale-date') {
                return formatWithPattern(dt, 'd', culture);
            }
        } else if (key === 'locale-datetime') {
            key = getEffectiveDateTimeFormat(key, ControlTypeDateTime);
            if (key === 'locale-datetime') {
                return formatWithPattern(dt, 'g', culture);
            }
        }

        if (key.startsWith(PatternPrefix)) {
            return formatWithPattern(dt, key.slice(PatternPrefix.length), culture);
        }

        switch (key) {
            case 'date-short':
                return formatWithTokens(dt, 'dd/MM/yyyy');
            case 'date-iso':
                return formatWithTokens(dt, 'yyyy-MM-dd');
            case 'date-long':
                return formatWithTokens(dt, 'dd MMM yyyy');
            case 'date-us':
                return formatWithTokens(dt, 'MM/dd/yyyy');
            case 'datetime-short':
                return formatWithTokens(dt, 'dd/MM/yyyy HH:mm');
            case 'datetime-full':
                return formatWithTokens(dt, 'dd/MM/yyyy HH:mm:ss');
            case 'datetime-iso':
                return formatWithTokens(dt, 'yyyy-MM-dd HH:mm');
            case 'locale-long':
                return formatWithPattern(dt, 'D', culture);
            case 'locale-long-datetime':
                return formatWithPattern(dt, 'G', culture);
            case 'locale-date':
                return formatWithPattern(dt, 'd', culture);
            case 'locale-datetime':
                return formatWithPattern(dt, 'g', culture);
            default:
                if (/[ydHm]/.test(key)) {
                    return formatWithPattern(dt, key, culture);
                }
                return formatLocaleShortDateTime(dt, culture);
        }
    }

    function formatValue(value, displayFormat, controlType) {
        if (value == null || value === '') return '';
        const dt = parseDate(value);
        if (!dt) return String(value);

        const formatKey = resolveFormatKey(displayFormat, controlType);
        if (!formatKey && !isTemporalControlType(controlType)) return String(value);

        return applyPreset(dt, formatKey || getDefaultForControlType(controlType), undefined, controlType);
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

    /** yyyy-MM-ddTHH:mm for input type="datetime-local". */
    function formatDateTimeInputValue(value) {
        if (value == null || value === '') return '';
        const dt = value instanceof Date ? value : parseDate(value);
        if (!dt) return String(value);
        return `${formatWithTokens(dt, 'yyyy-MM-dd')}T${formatWithTokens(dt, 'HH:mm')}`;
    }

    function getEffectiveFormatKey(isDateTime) {
        const locale = getLocaleConfig();
        return isDateTime
            ? (locale.dateTimeFormat || 'locale-datetime')
            : (locale.dateFormat || 'locale-date');
    }

    function resolvePattern(formatKey, isDateTime) {
        const key = (formatKey || '').toString();
        if (key.startsWith(PatternPrefix)) {
            return key.slice(PatternPrefix.length);
        }

        const patterns = {
            'date-short': 'dd/MM/yyyy',
            'date-iso': 'yyyy-MM-dd',
            'date-long': 'dd MMM yyyy',
            'date-us': 'MM/dd/yyyy',
            'datetime-short': 'dd/MM/yyyy HH:mm',
            'datetime-full': 'dd/MM/yyyy HH:mm:ss',
            'datetime-iso': 'yyyy-MM-dd HH:mm'
        };

        if (patterns[key]) return patterns[key];
        if (key === 'locale-date') return 'd';
        if (key === 'locale-long') return 'D';
        if (key === 'locale-datetime') return 'g';
        if (key === 'locale-long-datetime') return 'G';
        if (/[ydHm]/.test(key)) return key;
        return isDateTime ? 'g' : 'd';
    }

    function parseTokenPattern(text, pattern) {
        const input = (text || '').toString().trim();
        if (!input || !pattern) return null;

        const monthNames = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];
        const tokens = [];
        let regex = '^';
        let index = 0;

        while (index < pattern.length) {
            if (pattern.startsWith('yyyy', index)) {
                tokens.push('year');
                regex += '(\\d{4})';
                index += 4;
            } else if (pattern.startsWith('yy', index)) {
                tokens.push('year2');
                regex += '(\\d{2})';
                index += 2;
            } else if (pattern.startsWith('MMM', index)) {
                tokens.push('monthName');
                regex += '([A-Za-z]{3,})';
                index += 3;
            } else if (pattern.startsWith('MM', index)) {
                tokens.push('month');
                regex += '(\\d{1,2})';
                index += 2;
            } else if (pattern.startsWith('dd', index)) {
                tokens.push('day');
                regex += '(\\d{1,2})';
                index += 2;
            } else if (pattern.startsWith('HH', index)) {
                tokens.push('hour');
                regex += '(\\d{1,2})';
                index += 2;
            } else if (pattern.startsWith('mm', index)) {
                tokens.push('minute');
                regex += '(\\d{1,2})';
                index += 2;
            } else if (pattern.startsWith('ss', index)) {
                tokens.push('second');
                regex += '(\\d{1,2})';
                index += 2;
            } else {
                regex += pattern[index].replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                index += 1;
            }
        }

        regex += '$';
        const match = input.match(new RegExp(regex, 'i'));
        if (!match) return null;

        let year;
        let month = 1;
        let day = 1;
        let hour = 0;
        let minute = 0;
        let second = 0;

        tokens.forEach(function (token, i) {
            const value = match[i + 1];
            if (value == null) return;

            switch (token) {
                case 'year':
                    year = parseInt(value, 10);
                    break;
                case 'year2':
                    year = 2000 + parseInt(value, 10);
                    break;
                case 'monthName': {
                    const idx = monthNames.indexOf(value.slice(0, 3).toLowerCase());
                    if (idx >= 0) month = idx + 1;
                    break;
                }
                case 'month':
                    month = parseInt(value, 10);
                    break;
                case 'day':
                    day = parseInt(value, 10);
                    break;
                case 'hour':
                    hour = parseInt(value, 10);
                    break;
                case 'minute':
                    minute = parseInt(value, 10);
                    break;
                case 'second':
                    second = parseInt(value, 10);
                    break;
            }
        });

        if (!year || month < 1 || month > 12 || day < 1 || day > 31) return null;
        return new Date(year, month - 1, day, hour, minute, second);
    }

    function parseLocalePattern(text, pattern, culture) {
        if (pattern === 'd' || pattern === 'g' || pattern === 'D' || pattern === 'G') {
            const cultureName = culture || getLocale();
            const candidates = cultureName && cultureName.toLowerCase().startsWith('en-us')
                ? ['MM/dd/yyyy', 'dd/MM/yyyy']
                : ['dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd'];

            if (pattern === 'g' || pattern === 'G') {
                candidates.unshift('dd/MM/yyyy HH:mm', 'MM/dd/yyyy HH:mm', 'yyyy-MM-dd HH:mm');
            }

            for (const candidate of candidates) {
                const dt = parseTokenPattern(text, candidate);
                if (dt) return dt;
            }
            return null;
        }

        return parseTokenPattern(text, pattern);
    }

    function parseFormattedDate(text, formatKey, culture, isDateTime) {
        const input = (text || '').toString().trim();
        if (!input) return null;

        const isoDate = input.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?)?/);
        if (isoDate) {
            return new Date(
                parseInt(isoDate[1], 10),
                parseInt(isoDate[2], 10) - 1,
                parseInt(isoDate[3], 10),
                parseInt(isoDate[4] || '0', 10),
                parseInt(isoDate[5] || '0', 10),
                parseInt(isoDate[6] || '0', 10));
        }

        const key = formatKey || getEffectiveFormatKey(!!isDateTime);
        const pattern = resolvePattern(key, !!isDateTime);
        const parsed = parseLocalePattern(input, pattern, culture);
        if (parsed) return parsed;

        const fallback = parseDate(input);
        return fallback && !isNaN(fallback.getTime()) ? fallback : null;
    }

    function stripHtml(value) {
        if (value == null || value === '') return '';
        const el = document.createElement('div');
        el.innerHTML = String(value);
        return (el.textContent || el.innerText || '').trim();
    }

    function buildSelectOptions(selected, controlType) {
        const sel = normalizeFormatKey(selected, controlType);
        const ct = (controlType || '').toString();
        const relevant = ct === ControlTypeDateTime
            ? PRESETS.filter(p => p.key === 'locale-datetime' || (p.group === 'Date & time' && p.key !== 'locale-datetime'))
            : ct === ControlTypeDate
                ? PRESETS.filter(p => p.key === 'locale-date' || (p.group === 'Date' && p.key !== 'locale-date'))
                : PRESETS;

        const systemKey = ct === ControlTypeDateTime ? 'locale-datetime' : 'locale-date';
        const systemLabel = 'System preference';
        const rows = [{ key: systemKey, label: systemLabel }].concat(
            relevant.filter(p => p.key && p.key !== systemKey).map(p => ({ key: p.key, label: p.label }))
        );

        return rows.map(p =>
            `<option value="${p.key}" ${sel === p.key ? 'selected' : ''}>${p.label}</option>`
        ).join('');
    }

    async function buildSelectOptionsAsync(selected, controlType, culture) {
        const sel = normalizeFormatKey(selected, controlType);
        const localeCulture = culture
            || (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.culture)
            || 'en-US';

        try {
            const apiUrl = '/api/metaforge/preferences/date-formats?culture=' + encodeURIComponent(localeCulture);
            const response = await fetch(apiUrl, { credentials: 'same-origin' });
            if (response.ok) {
                const payload = await response.json();
                const options = ctOptions(controlType, payload);
                return options.map(function (option) {
                    const label = option.Sample
                        ? `${option.Label} — ${option.Sample}`
                        : option.Label;
                    return `<option value="${option.Key}" ${sel === option.Key ? 'selected' : ''}>${label}</option>`;
                }).join('');
            }
        } catch (_) { /* fallback below */ }

        return buildSelectOptions(selected, controlType);
    }

    function buildSystemPreferenceOption(controlType, payload) {
        const ct = (controlType || '').toString();
        const isDateTime = ct === ControlTypeDateTime;
        const systemKey = isDateTime ? 'locale-datetime' : 'locale-date';
        const effectiveKey = isDateTime
            ? (payload.effectiveDateTimeFormat || 'locale-datetime')
            : (payload.effectiveDateFormat || 'locale-date');
        const catalog = isDateTime
            ? (payload.dateTimeFormats || [])
            : (payload.dateFormats || []);
        const match = catalog.find(o => o.Key === effectiveKey);
        return {
            Key: systemKey,
            Label: 'System preference',
            Sample: match?.Sample || '',
            Group: 'General'
        };
    }

    function ctOptions(controlType, payload) {
        const ct = (controlType || '').toString();
        const options = [buildSystemPreferenceOption(controlType, payload)];
        const seen = new Set(options.map(o => o.Key));

        const append = list => {
            (list || []).forEach(option => {
                if (seen.has(option.Key)) return;
                if (option.Key === 'locale-date' || option.Key === 'locale-datetime') return;
                seen.add(option.Key);
                options.push(option);
            });
        };

        if (ct === ControlTypeDateTime) {
            append(payload.dateTimeFormats);
        } else if (ct === ControlTypeDate) {
            append(payload.dateFormats);
        } else {
            append(payload.dateFormats);
            append(payload.dateTimeFormats);
        }

        return options;
    }

    return {
        PRESETS,
        getDefaultForControlType,
        normalizeFormatKey,
        usesSystemPreference,
        isTemporalControlType,
        resolveFormatKey,
        formatValue,
        formatWithKey: applyPreset,
        formatDateInputValue,
        formatDateTimeInputValue,
        getEffectiveFormatKey,
        resolvePattern,
        parseFormattedDate,
        buildSelectOptions,
        buildSelectOptionsAsync,
        stripHtml
    };
})();
