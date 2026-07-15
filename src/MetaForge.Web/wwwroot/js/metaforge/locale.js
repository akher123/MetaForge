/**
 * Culture/locale preferences — format dates with effective culture, sync to API.
 */
(function () {
    const cultureApiUrl = '/api/metaforge/preferences/culture';
    const dateFormatsApiUrl = '/api/metaforge/preferences/date-formats';
    const dateFormatApiUrl = '/api/metaforge/preferences/date-formats';    const sampleDate = new Date(2026, 6, 15, 14, 30, 0);
    const sampleNumber = 1234567.89;
    let saving = false;
    let cachedFormatOptions = null;

    function getEffectiveCulture() {
        if (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.culture) {
            return window.__METAFORGE_LOCALE__.culture;
        }
        return undefined;
    }

    function resolveCultureForFormatting(cultureOverride) {
        const select = document.getElementById('culturePickerSelect');
        const systemDefault = select
            ? select.getAttribute('data-system-default')
            : (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.systemDefaultCulture);

        if (!cultureOverride) {
            return systemDefault || getEffectiveCulture() || undefined;
        }
        return cultureOverride;
    }

    function getPreviewDateFormat() {
        const select = document.getElementById('dateFormatPickerSelect');
        const value = select ? (select.value || '').trim() : '';
        if (!value) {
            return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.dateFormat) || 'locale-date';
        }
        return value;
    }

    function getPreviewDateTimeFormat() {
        const select = document.getElementById('dateTimeFormatPickerSelect');
        const value = select ? (select.value || '').trim() : '';
        if (!value) {
            return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.dateTimeFormat) || 'locale-datetime';
        }
        return value;
    }

    function getEffectiveDateFormat() {
        return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.dateFormat) || 'locale-date';
    }

    function getEffectiveDateTimeFormat() {
        return (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.dateTimeFormat) || 'locale-datetime';
    }

    function formatLocaleDate(dt, culture, formatKey) {
        if (window.MetaForgeGridDisplayFormat && typeof window.MetaForgeGridDisplayFormat.formatWithKey === 'function') {
            const key = formatKey || getEffectiveDateFormat();
            return window.MetaForgeGridDisplayFormat.formatWithKey(dt, key, culture);
        }
        try {
            return new Intl.DateTimeFormat(culture, { dateStyle: 'short' }).format(dt);
        } catch (_) {
            return dt.toLocaleDateString(culture || undefined);
        }
    }

    function formatLocaleDateTime(dt, culture, formatKey) {
        if (window.MetaForgeGridDisplayFormat && typeof window.MetaForgeGridDisplayFormat.formatWithKey === 'function') {
            const key = formatKey || getEffectiveDateTimeFormat();
            return window.MetaForgeGridDisplayFormat.formatWithKey(dt, key, culture);
        }
        try {
            return new Intl.DateTimeFormat(culture, { dateStyle: 'short', timeStyle: 'short' }).format(dt);
        } catch (_) {
            return dt.toLocaleString(culture || undefined);
        }
    }

    function formatLocaleNumber(value, culture) {
        try {
            return new Intl.NumberFormat(culture, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
        } catch (_) {
            return Number(value).toLocaleString(culture || undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
    }

    function applyDocumentLocale(culture, isRtl) {
        const root = document.documentElement;
        if (culture) root.setAttribute('lang', culture);
        root.setAttribute('dir', isRtl ? 'rtl' : 'ltr');

        if (window.__METAFORGE_LOCALE__) {
            window.__METAFORGE_LOCALE__.culture = culture;
            window.__METAFORGE_LOCALE__.isRtl = !!isRtl;
        }
    }

    function updatePreview(culture) {
        const resolved = resolveCultureForFormatting(culture);
        const dateText = formatLocaleDate(sampleDate, resolved, getPreviewDateFormat());
        const dateTimeText = formatLocaleDateTime(sampleDate, resolved, getPreviewDateTimeFormat());
        const numberText = formatLocaleNumber(sampleNumber, resolved);

        ['culturePreviewDate', 'preferencesPreviewDate'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) el.textContent = dateText;
        });
        ['culturePreviewDateTime', 'preferencesPreviewDateTime'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) el.textContent = dateTimeText;
        });
        ['culturePreviewNumber', 'preferencesPreviewNumber'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) el.textContent = numberText;
        });
    }

    function updateOverrideBadge(hasOverrides) {
        const badge = document.querySelector('.culture-picker-override-badge');
        if (!badge) return;

        badge.textContent = hasOverrides ? 'Custom' : 'System default';
        badge.classList.toggle('culture-picker-override-badge--inherit', !hasOverrides);

        const resetBtn = document.getElementById('culturePickerResetBtn');
        if (resetBtn) resetBtn.disabled = !hasOverrides;
    }

    function hasEffectiveOverrides(effective) {
        if (!effective) return false;
        return !!(effective.cultureIsUserOverride
            || effective.dateFormatIsUserOverride
            || effective.dateTimeFormatIsUserOverride);
    }

    function setSavingState(isSaving) {
        saving = isSaving;
        const status = document.getElementById('culturePickerStatus');
        if (status) status.hidden = !isSaving;

        ['culturePickerSelect', 'dateFormatPickerSelect', 'dateTimeFormatPickerSelect'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) el.disabled = isSaving;
        });

        const resetBtn = document.getElementById('culturePickerResetBtn');
        if (resetBtn && !resetBtn.disabled) {
            resetBtn.disabled = isSaving;
            resetBtn.dataset.wasEnabled = '1';
        } else if (resetBtn) {
            resetBtn.dataset.wasEnabled = '0';
        }
    }

    function restoreResetButtonState() {
        const resetBtn = document.getElementById('culturePickerResetBtn');
        if (resetBtn && resetBtn.dataset.wasEnabled === '1') {
            resetBtn.disabled = false;
            delete resetBtn.dataset.wasEnabled;
        }
    }

    function populateFormatSelect(select, options, preferredKey) {
        if (!select) return;
        const inheritOption = select.querySelector('option[value=""]');
        const selected = preferredKey || select.dataset.selected || '';
        select.innerHTML = '';
        if (inheritOption) {
            select.appendChild(inheritOption);
        } else {
            const inherit = document.createElement('option');
            inherit.value = '';
            inherit.textContent = 'Use system default';
            select.appendChild(inherit);
        }

        for (const option of options) {
            const el = document.createElement('option');
            el.value = option.Key;
            el.textContent = `${option.Label} — ${option.Sample}`;
            if (option.Key === selected) {
                el.selected = true;
            }
            select.appendChild(el);
        }

        if (!select.value && selected) {
            select.value = selected;
        }
    }

    async function loadDateFormatOptions(culture) {
        const cultureSelect = document.getElementById('culturePickerSelect');
        const resolvedCulture = culture || resolveCultureForFormatting((cultureSelect?.value || '').trim() || null);
        if (!resolvedCulture) return cachedFormatOptions;

        const dateSelect = document.getElementById('dateFormatPickerSelect');
        const dateTimeSelect = document.getElementById('dateTimeFormatPickerSelect');
        const previousDate = dateSelect?.value || dateSelect?.dataset.selected || '';
        const previousDateTime = dateTimeSelect?.value || dateTimeSelect?.dataset.selected || '';

        const response = await fetch(dateFormatsApiUrl + '?culture=' + encodeURIComponent(resolvedCulture), {
            credentials: 'same-origin'
        });
        if (!response.ok) return cachedFormatOptions;

        const payload = await response.json();
        cachedFormatOptions = payload;

        const dateKeys = new Set((payload.dateFormats || []).map(o => o.Key));
        const dateTimeKeys = new Set((payload.dateTimeFormats || []).map(o => o.Key));

        populateFormatSelect(
            dateSelect,
            payload.dateFormats || [],
            previousDate && dateKeys.has(previousDate) ? previousDate : '');
        populateFormatSelect(
            dateTimeSelect,
            payload.dateTimeFormats || [],
            previousDateTime && dateTimeKeys.has(previousDateTime) ? previousDateTime : '');

        updatePreview((cultureSelect?.value || '').trim() || null);
        return payload;
    }

    function persistCulture(cultureOverride) {
        if (!window.__METAFORGE_LOCALE__ || !window.__METAFORGE_LOCALE__.authenticated) {
            return Promise.resolve();
        }

        setSavingState(true);
        return fetch(cultureApiUrl, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ culture: cultureOverride || null })
        }).then(function (res) {
            if (!res.ok) throw new Error('Failed to save culture');
            return res.json();
        }).finally(function () {
            setSavingState(false);
            restoreResetButtonState();
        });
    }

    function persistDateFormats() {
        if (!window.__METAFORGE_LOCALE__ || !window.__METAFORGE_LOCALE__.authenticated) {
            return Promise.resolve();
        }

        const dateFormat = (document.getElementById('dateFormatPickerSelect')?.value || '').trim();
        const dateTimeFormat = (document.getElementById('dateTimeFormatPickerSelect')?.value || '').trim();

        setSavingState(true);
        return fetch(dateFormatApiUrl, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({
                dateFormat: dateFormat || null,
                dateTimeFormat: dateTimeFormat || null
            })
        }).then(function (res) {
            if (!res.ok) throw new Error('Failed to save date formats');
            return res.json();
        }).finally(function () {
            setSavingState(false);
            restoreResetButtonState();
        });
    }

    function resetLocaleOverrides() {
        if (!window.__METAFORGE_LOCALE__ || !window.__METAFORGE_LOCALE__.authenticated) {
            return Promise.resolve();
        }

        setSavingState(true);
        return fetch(cultureApiUrl, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ culture: null })
        }).then(function (res) {
            if (!res.ok) throw new Error('Failed to reset culture');
            return fetch(dateFormatApiUrl, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify({ dateFormat: null, dateTimeFormat: null })
            });
        }).then(function (res) {
            if (!res.ok) throw new Error('Failed to reset date formats');
            return res.json();
        }).finally(function () {
            setSavingState(false);
            restoreResetButtonState();
        });
    }

    function notifySuccess(message) {
        if (window.MetaForgeUi && typeof window.MetaForgeUi.showAlert === 'function') {
            window.MetaForgeUi.showAlert(message || 'Preferences updated. Reloading…', 'success', 2000);
        }
    }

    function notifyError(message) {
        if (window.MetaForgeUi && typeof window.MetaForgeUi.showAlert === 'function') {
            window.MetaForgeUi.showAlert(message || 'Could not save preference.', 'danger', 5000);
        }
    }

    function reloadSoon() {
        setTimeout(function () { window.location.reload(); }, 400);
    }

    function onCultureChange() {
        const select = document.getElementById('culturePickerSelect');
        if (!select || saving) return;

        const cultureOverride = (select.value || '').trim();
        loadDateFormatOptions(resolveCultureForFormatting(cultureOverride || null))
            .then(function () {
                updatePreview(cultureOverride || null);
                return persistCulture(cultureOverride || null);
            })
            .then(function () {
                notifySuccess('Culture updated. Reloading…');
                reloadSoon();
            })
            .catch(function () { notifyError('Could not save culture preference.'); });
    }

    function onDateFormatChange() {
        if (saving) return;
        updatePreview((document.getElementById('culturePickerSelect')?.value || '').trim() || null);
        persistDateFormats()
            .then(function (effective) {
                updateOverrideBadge(hasEffectiveOverrides(effective));
                notifySuccess('Date format updated. Reloading…');
                reloadSoon();
            })
            .catch(function () { notifyError('Could not save date format preference.'); });
    }

    function onResetLocale() {
        if (saving) return;
        if (!window.confirm('Reset culture and date formats to system defaults?')) return;

        resetLocaleOverrides()
            .then(function () {
                notifySuccess('Preferences reset. Reloading…');
                reloadSoon();
            })
            .catch(function () { notifyError('Could not reset preferences.'); });
    }

    function bindCulturePicker() {
        const select = document.getElementById('culturePickerSelect');
        if (!select || select.dataset.cultureBound === '1') return;
        select.dataset.cultureBound = '1';

        if (window.jQuery && window.jQuery.fn.select2) {
            window.jQuery(select).select2({ theme: 'bootstrap-5', width: '100%' });
            window.jQuery(select).on('change', onCultureChange);
        } else {
            select.addEventListener('change', onCultureChange);
        }

        select.addEventListener('focus', function () {
            updatePreview((select.value || '').trim() || null);
        });

        const dateFormatSelect = document.getElementById('dateFormatPickerSelect');
        const dateTimeFormatSelect = document.getElementById('dateTimeFormatPickerSelect');

        if (dateFormatSelect) {
            dateFormatSelect.addEventListener('change', onDateFormatChange);
        }
        if (dateTimeFormatSelect) {
            dateTimeFormatSelect.addEventListener('change', onDateFormatChange);
        }

        const resetBtn = document.getElementById('culturePickerResetBtn');
        if (resetBtn) {
            resetBtn.addEventListener('click', onResetLocale);
        }

        loadDateFormatOptions(resolveCultureForFormatting((select.value || '').trim() || null));
    }

    function init() {
        if (window.__METAFORGE_LOCALE__) {
            applyDocumentLocale(
                window.__METAFORGE_LOCALE__.culture,
                window.__METAFORGE_LOCALE__.isRtl);
        }
        bindCulturePicker();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.MetaForgeLocale = {
        getCulture: getEffectiveCulture,
        formatDate: function (value) {
            const dt = value instanceof Date ? value : new Date(value);
            if (isNaN(dt.getTime())) return '';
            return formatLocaleDate(dt, getEffectiveCulture(), getEffectiveDateFormat());
        },
        formatDateTime: function (value) {
            const dt = value instanceof Date ? value : new Date(value);
            if (isNaN(dt.getTime())) return '';
            return formatLocaleDateTime(dt, getEffectiveCulture(), getEffectiveDateTimeFormat());
        },
        formatNumber: function (value, options) {
            const opts = options || { maximumFractionDigits: 4 };
            const culture = getEffectiveCulture();
            try {
                return new Intl.NumberFormat(culture, opts).format(value);
            } catch (_) {
                return Number(value).toLocaleString(culture || undefined, opts);
            }
        },
        updatePreview: updatePreview,
        formatLocaleDate: formatLocaleDate,
        formatLocaleDateTime: formatLocaleDateTime,
        formatLocaleNumber: formatLocaleNumber,
        loadDateFormatOptions: loadDateFormatOptions
    };
})();
