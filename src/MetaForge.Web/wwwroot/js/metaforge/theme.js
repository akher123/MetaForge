/**
 * Multi-theme UI — apply data-theme, sync to API + localStorage.
 */
(function () {
    const storageKey = 'metaforge.theme';
    const apiUrl = '/api/metaforge/preferences/theme';
    let saving = false;

    function readStoredTheme() {
        try {
            return localStorage.getItem(storageKey);
        } catch {
            return null;
        }
    }

    function writeStoredTheme(key) {
        try {
            localStorage.setItem(storageKey, key);
        } catch (_) { /* ignore */ }
    }

    function resolveBootstrapMode(themeKey, isDarkHint) {
        if (typeof isDarkHint === 'boolean') return isDarkHint ? 'dark' : 'light';
        return themeKey && themeKey.indexOf('dark') >= 0 ? 'dark' : 'light';
    }

    function syncPickerState(activeKey) {
        document.querySelectorAll('.theme-picker-grid[data-theme-mode="user"] .theme-card').forEach(function (btn) {
            const key = btn.getAttribute('data-theme-key');
            const active = key === activeKey;
            btn.classList.toggle('is-active', active);
            btn.setAttribute('aria-selected', active ? 'true' : 'false');
        });
    }

    function syncPickerStateForGrid(grid, activeKey) {
        if (!grid) return;
        grid.querySelectorAll('.theme-card').forEach(function (btn) {
            const key = btn.getAttribute('data-theme-key');
            const active = key === activeKey;
            btn.classList.toggle('is-active', active);
            btn.setAttribute('aria-selected', active ? 'true' : 'false');
        });
    }

    function updateToolbarLabel(themeName) {
        const trigger = document.getElementById('appThemeTrigger');
        if (!trigger) return;

        const label = trigger.querySelector('.app-theme-trigger-label');
        if (label && themeName) label.textContent = themeName;

        if (themeName) {
            trigger.setAttribute('aria-label', 'Change color theme, current: ' + themeName);
            trigger.setAttribute('title', 'Appearance: ' + themeName);
        }
    }

    function setSavingState(isSaving) {
        saving = isSaving;
        const status = document.getElementById('themePickerStatus');
        if (status) status.hidden = !isSaving;

        document.querySelectorAll('.theme-picker-grid[data-theme-mode="user"] .theme-card').forEach(function (btn) {
            btn.disabled = isSaving;
            btn.classList.toggle('is-saving', isSaving);
        });
    }

    function refreshThemedChrome() {
        requestAnimationFrame(function () {
            document.querySelectorAll(
                '.dataTables_scrollHead thead th, table.dataTable thead th, .table > thead th, .master-detail-grid thead th, .admin-modal .modal-header, .module-grid-toolbar .btn-primary, #btnAdd, #btnAddMasterDetail, .btn-teal, #btnAddDetail'
            ).forEach(function (el) {
                el.style.removeProperty('background');
                el.style.removeProperty('background-color');
                el.style.removeProperty('background-image');
                el.style.removeProperty('border-color');
                el.style.removeProperty('color');
            });

            if (window.MetaForgeDataTables && typeof MetaForgeDataTables.adjustVisibleColumns === 'function') {
                MetaForgeDataTables.adjustVisibleColumns();
            }

            document.dispatchEvent(new CustomEvent('metaforge-theme-changed', {
                detail: { theme: document.documentElement.getAttribute('data-theme') }
            }));
        });
    }

    function applyTheme(themeKey, isDarkHint, themeName) {
        const key = themeKey || 'indigo-light';
        const root = document.documentElement;
        root.setAttribute('data-theme', key);
        root.setAttribute('data-bs-theme', resolveBootstrapMode(key, isDarkHint));
        writeStoredTheme(key);
        syncPickerState(key);

        if (window.__METAFORGE_THEME__) {
            window.__METAFORGE_THEME__.key = key;
            window.__METAFORGE_THEME__.isDark = resolveBootstrapMode(key, isDarkHint) === 'dark';
        }

        if (themeName) updateToolbarLabel(themeName);
        refreshThemedChrome();
    }

    function getInitialTheme() {
        if (window.__METAFORGE_THEME__ && window.__METAFORGE_THEME__.key) {
            return {
                key: window.__METAFORGE_THEME__.key,
                isDark: window.__METAFORGE_THEME__.isDark,
                name: null
            };
        }
        const stored = readStoredTheme();
        if (stored) return { key: stored, isDark: null, name: null };
        return { key: 'indigo-light', isDark: false, name: 'Indigo Light' };
    }

    function persistTheme(themeKey) {
        if (!window.__METAFORGE_THEME__ || !window.__METAFORGE_THEME__.authenticated) {
            return Promise.resolve();
        }

        setSavingState(true);
        return fetch(apiUrl, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ themeKey: themeKey })
        }).then(function (res) {
            if (!res.ok) throw new Error('Failed to save theme');
            return res.json();
        }).finally(function () {
            setSavingState(false);
        });
    }

    function notifySuccess(themeName) {
        const msg = themeName ? themeName + ' theme applied.' : 'Theme applied.';
        if (window.MetaForgeUi && typeof window.MetaForgeUi.showAlert === 'function') {
            window.MetaForgeUi.showAlert(msg, 'success', 2800);
        }
    }

    function notifyError() {
        if (window.MetaForgeUi && typeof window.MetaForgeUi.showAlert === 'function') {
            window.MetaForgeUi.showAlert('Could not save theme preference.', 'danger', 5000);
        }
    }

    function onPickerClick(ev) {
        const btn = ev.target.closest('.theme-card');
        if (!btn || btn.disabled || saving) return;

        const grid = btn.closest('.theme-picker-grid');
        if (!grid) return;

        const mode = grid.getAttribute('data-theme-mode') || 'user';
        const themeKey = btn.getAttribute('data-theme-key');
        if (!themeKey) return;

        if (btn.classList.contains('is-active')) return;

        if (mode === 'system') {
            syncPickerStateForGrid(grid, themeKey);
            const hidden = document.getElementById('defaultThemeKey');
            if (hidden) hidden.value = themeKey;
            grid.dispatchEvent(new CustomEvent('metaforge-system-theme-selected', {
                bubbles: true,
                detail: { themeKey: themeKey, themeName: btn.getAttribute('data-theme-name') || '' }
            }));
            return;
        }

        const isDark = btn.getAttribute('data-theme-dark') === 'true';
        const themeName = btn.getAttribute('data-theme-name') || '';

        applyTheme(themeKey, isDark, themeName);

        persistTheme(themeKey)
            .then(function () { notifySuccess(themeName); })
            .catch(notifyError);
    }

    function bindPickers() {
        document.querySelectorAll('.theme-picker-grid').forEach(function (grid) {
            if (grid.dataset.themeBound === '1') return;
            grid.dataset.themeBound = '1';
            grid.addEventListener('click', onPickerClick);
        });
    }

    function initToolbarLabel() {
        const active = document.querySelector('.theme-card.is-active');
        if (active) {
            updateToolbarLabel(active.getAttribute('data-theme-name'));
        }
    }

    function init() {
        const initial = getInitialTheme();
        applyTheme(initial.key, initial.isDark, initial.name);
        bindPickers();
        initToolbarLabel();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.MetaForgeTheme = {
        apply: applyTheme,
        persist: persistTheme,
        bindPickers: bindPickers,
        refreshChrome: refreshThemedChrome,
        syncPickerStateForGrid: syncPickerStateForGrid
    };
})();
