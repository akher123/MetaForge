/**
 * Sidebar tree — persist expanded folders, highlight active route, collapse/expand rail.
 */
(function () {
    const folderStorageKey = 'admin.sidebar.openFolders';
    const collapseStorageKey = 'metaforge.sidebar.collapsed';
    const mobileQuery = window.matchMedia('(max-width: 991.98px)');

    function loadOpenFolders() {
        try {
            return JSON.parse(sessionStorage.getItem(folderStorageKey) || '[]');
        } catch {
            return [];
        }
    }

    function saveOpenFolders(ids) {
        sessionStorage.setItem(folderStorageKey, JSON.stringify(ids));
    }

    function isCollapsed() {
        return document.documentElement.classList.contains('sidebar-collapsed');
    }

    function isMobileSidebar() {
        return mobileQuery.matches;
    }

    function setCollapsed(collapsed) {
        if (isMobileSidebar()) {
            collapsed = false;
        }

        document.documentElement.classList.toggle('sidebar-collapsed', collapsed);

        try {
            localStorage.setItem(collapseStorageKey, collapsed ? '1' : '0');
        } catch (_) { /* ignore */ }

        const toggle = document.getElementById('sidebarCollapseToggle');
        if (toggle) {
            toggle.setAttribute('aria-expanded', (!collapsed).toString());
            toggle.setAttribute(
                'aria-label',
                collapsed ? 'Expand sidebar menu' : 'Collapse sidebar menu'
            );
            toggle.setAttribute('title', collapsed ? 'Expand sidebar menu' : 'Collapse sidebar menu');
        }

        if (collapsed) {
            document.querySelectorAll('.sidebar-folder[open]').forEach(function (folder) {
                folder.removeAttribute('open');
            });
        }
    }

    function getMobileToggle() {
        return document.getElementById('sidebarMobileToggle');
    }

    function isMobileSidebarOpen() {
        const sidebar = document.getElementById('sidebarNav');
        return !!sidebar && sidebar.classList.contains('show');
    }

    function setMobileToggleState(open) {
        const mobileToggle = getMobileToggle();
        if (!mobileToggle) {
            return;
        }

        mobileToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        mobileToggle.classList.toggle('collapsed', !open);
        mobileToggle.setAttribute(
            'aria-label',
            open ? 'Close navigation menu' : 'Open navigation menu'
        );
    }

    function syncMobileSidebarA11y(open) {
        const sidebar = document.getElementById('sidebarNav');
        if (!sidebar) {
            return;
        }

        if (isMobileSidebar()) {
            sidebar.setAttribute('aria-hidden', open ? 'false' : 'true');
        } else {
            sidebar.removeAttribute('aria-hidden');
        }
    }

    function openMobileSidebar() {
        const sidebar = document.getElementById('sidebarNav');
        if (!sidebar || !isMobileSidebar()) {
            return;
        }

        sidebar.classList.add('show');
        syncMobileBackdrop(true);
        setMobileToggleState(true);
        syncMobileSidebarA11y(true);
    }

    function closeMobileSidebar() {
        const sidebar = document.getElementById('sidebarNav');
        if (!sidebar || !isMobileSidebar()) {
            return;
        }

        sidebar.classList.remove('show');
        syncMobileBackdrop(false);
        setMobileToggleState(false);
        syncMobileSidebarA11y(false);
    }

    function toggleMobileSidebar() {
        if (isMobileSidebarOpen()) {
            closeMobileSidebar();
        } else {
            openMobileSidebar();
        }
    }

    function syncMobileBackdrop(show) {
        const backdrop = document.getElementById('sidebarBackdrop');
        if (!backdrop) {
            return;
        }

        backdrop.classList.toggle('show', !!show);
        backdrop.setAttribute('aria-hidden', show ? 'false' : 'true');
        document.body.classList.toggle('sidebar-drawer-open', !!show);
    }

    function initFolderPersistence() {
        const openIds = loadOpenFolders();
        document.querySelectorAll('.sidebar-folder').forEach(function (folder) {
            const key = folder.querySelector('.sidebar-label')?.textContent?.trim() || '';
            if (openIds.includes(key) || folder.querySelector('.sidebar-link.active')) {
                folder.setAttribute('open', 'open');
            }

            folder.addEventListener('toggle', function () {
                if (isCollapsed()) {
                    return;
                }

                const names = loadOpenFolders();
                if (folder.open && key && !names.includes(key)) {
                    names.push(key);
                } else if (!folder.open && key) {
                    const idx = names.indexOf(key);
                    if (idx >= 0) {
                        names.splice(idx, 1);
                    }
                }
                saveOpenFolders(names);
            });
        });
    }

    function initCollapsedFlyouts() {
        document.querySelectorAll('.sidebar-folder-toggle').forEach(function (summary) {
            summary.addEventListener('click', function (event) {
                if (isCollapsed()) {
                    event.preventDefault();
                }
            });
        });
    }

    function initCollapseToggle() {
        const toggle = document.getElementById('sidebarCollapseToggle');
        if (!toggle) {
            return;
        }

        const initialCollapsed = (function () {
            try {
                return localStorage.getItem(collapseStorageKey) === '1';
            } catch {
                return false;
            }
        })();

        setCollapsed(initialCollapsed);

        toggle.addEventListener('click', function () {
            setCollapsed(!isCollapsed());
        });
    }

    function initMobileSidebar() {
        const sidebar = document.getElementById('sidebarNav');
        const backdrop = document.getElementById('sidebarBackdrop');
        const mobileToggle = getMobileToggle();

        if (!sidebar) {
            return;
        }

        closeMobileSidebar();

        if (mobileToggle) {
            mobileToggle.addEventListener('click', function () {
                toggleMobileSidebar();
            });
        }

        if (backdrop) {
            backdrop.addEventListener('click', closeMobileSidebar);
        }

        sidebar.querySelectorAll('.sidebar-link').forEach(function (link) {
            link.addEventListener('click', function () {
                closeMobileSidebar();
            });
        });

        mobileQuery.addEventListener('change', function () {
            if (!isMobileSidebar()) {
                sidebar.classList.remove('show');
                syncMobileBackdrop(false);
                document.body.classList.remove('sidebar-drawer-open');
                syncMobileSidebarA11y(false);
                setMobileToggleState(false);
                return;
            }

            setCollapsed(false);
            closeMobileSidebar();
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                closeMobileSidebar();
            }
        });
    }

    function init() {
        initFolderPersistence();
        initCollapsedFlyouts();
        initCollapseToggle();
        initMobileSidebar();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
