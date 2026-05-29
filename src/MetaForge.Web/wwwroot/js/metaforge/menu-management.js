/**
 * Menu management — create/edit/delete navigation entries.
 */
const MenuManagement = (function () {
    const defaultIcons = {
        Folder: 'fa-folder',
        Form: 'fa-table',
        Url: 'fa-link'
    };

    function init() {
        if ($('#menuFormApp').length) {
            initForm();
        }

        if ($('#menuIndexApp').length) {
            initIndex();
        }
    }

    function initIndex() {
        if (!$('#menuTable').length) {
            return;
        }

        let menuTableApi = null;

        function isMenuTableSettings(settings) {
            return settings.sTableId === 'menuTable'
                || (settings.nTable && settings.nTable.id === 'menuTable');
        }

        function getMenuRowById(id) {
            return document.querySelector('#menuTable tbody tr.menu-tree-row[data-menu-id="' + id + '"]');
        }

        function isMenuTreeRowVisible(row) {
            if (!row) {
                return true;
            }

            let parentId = row.getAttribute('data-parent-id');
            while (parentId) {
                const parent = getMenuRowById(parentId);
                if (!parent) {
                    break;
                }
                if (parent.getAttribute('data-expanded') === 'false') {
                    return false;
                }
                parentId = parent.getAttribute('data-parent-id') || '';
            }

            return true;
        }

        function setMenuTreeChevron(row, expanded) {
            const toggle = row.querySelector('.menu-tree-toggle');
            if (!toggle) {
                return;
            }

            const icon = toggle.querySelector('.menu-tree-chevron');
            if (icon) {
                icon.classList.remove('fa-chevron-down', 'fa-chevron-right');
                icon.classList.add(expanded ? 'fa-chevron-down' : 'fa-chevron-right');
            }

            toggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        }

        function refreshMenuTreeVisibility() {
            if (menuTableApi) {
                menuTableApi.draw(false);
            }
        }

        function setMenuRowExpanded(row, expanded) {
            row.setAttribute('data-expanded', expanded ? 'true' : 'false');
            setMenuTreeChevron(row, expanded);
            refreshMenuTreeVisibility();
        }

        function setAllMenuRowsExpanded(expanded) {
            document.querySelectorAll('#menuTable tbody tr.menu-tree-row[data-has-children="true"]').forEach(function (row) {
                row.setAttribute('data-expanded', expanded ? 'true' : 'false');
                setMenuTreeChevron(row, expanded);
            });
            refreshMenuTreeVisibility();
        }

        $.fn.dataTable.ext.search.push(function (settings, _data, dataIndex) {
            if (!isMenuTableSettings(settings)) {
                return true;
            }

            return isMenuTreeRowVisible(settings.aoData[dataIndex].nTr);
        });

        $('#menuIndexApp').on('click', '.menu-tree-toggle', function (e) {
            e.preventDefault();
            e.stopPropagation();

            const row = this.closest('tr.menu-tree-row');
            if (!row) {
                return;
            }

            const expanded = row.getAttribute('data-expanded') !== 'false';
            setMenuRowExpanded(row, !expanded);
        });

        $('#btnMenuExpandAll').on('click', function () {
            setAllMenuRowsExpanded(true);
        });

        $('#btnMenuCollapseAll').on('click', function () {
            setAllMenuRowsExpanded(false);
        });

        menuTableApi = $('#menuTable').DataTable({
            paging: false,
            ordering: false,
            processing: false,
            metaforgeProgress: false,
            columnDefs: [{ orderable: false, targets: -1 }],
            language: { search: 'Filter menu:' }
        });

        document.querySelectorAll('#menuTable tbody tr.menu-tree-row[data-has-children="true"]').forEach(function (row) {
            setMenuTreeChevron(row, true);
        });

        refreshMenuTreeVisibility();

        $('#menuIndexApp').on('click', '.btn-delete-menu', function () {
            deleteMenu($(this).data('id'), $(this).data('name'));
        });
    }

    function initForm() {
        bindTypeOptions();
        bindIconPreview();
        toggleTypeFields();
        updateIconPreview();

        $('#btnSaveMenu').on('click', saveMenu);
    }

    function bindTypeOptions() {
        $('.menu-type-option').on('click', function () {
            const type = $(this).data('type');
            $('#itemType').val(type);
            $('.menu-type-option').removeClass('is-selected');
            $(this).addClass('is-selected');
            $(this).find('input[type="radio"]').prop('checked', true);
            toggleTypeFields();
            updateIconPreview();
        });
    }

    function bindIconPreview() {
        $('#menuIcon').on('input', updateIconPreview);
    }

    function normalizeIconClass(value, fallbackType) {
        const raw = (value || '').trim();
        if (!raw) {
            return 'fa-solid ' + (defaultIcons[fallbackType] || defaultIcons.Folder);
        }

        if (raw.includes(' ')) {
            return raw.startsWith('fa-') && !raw.includes('fa-solid') && !raw.includes('fa-regular')
                ? 'fa-solid ' + raw
                : raw;
        }

        return 'fa-solid ' + raw;
    }

    function updateIconPreview() {
        const type = $('#itemType').val() || 'Folder';
        const iconClass = normalizeIconClass($('#menuIcon').val(), type);
        $('#menuIconPreview').attr('class', iconClass);
    }

    function toggleTypeFields() {
        const type = $('#itemType').val();
        const $modulePanel = $('.menu-link-panel.module-fields');
        const $urlPanel = $('.menu-link-panel.url-fields');

        $modulePanel.toggle(type === 'Form');
        $urlPanel.toggle(type === 'Url');

        if (type !== 'Form') {
            $modulePanel.attr('hidden', 'hidden');
        } else {
            $modulePanel.removeAttr('hidden');
        }

        if (type !== 'Url') {
            $urlPanel.attr('hidden', 'hidden');
        } else {
            $urlPanel.removeAttr('hidden');
        }
    }

    function saveMenu() {
        const payload = {
            id: parseInt($('#menuId').val(), 10) || 0,
            parentId: $('#parentId').val() ? parseInt($('#parentId').val(), 10) : null,
            name: $('#menuName').val()?.trim(),
            icon: $('#menuIcon').val()?.trim() || null,
            itemType: $('#itemType').val(),
            formId: $('#moduleId').val() ? parseInt($('#moduleId').val(), 10) : null,
            action: $('#menuAction').val(),
            url: $('#menuUrl').val()?.trim() || null,
            displayOrder: parseInt($('#displayOrder').val(), 10) || 0,
            isActive: $('#isActive').is(':checked')
        };

        const $btn = $('#btnSaveMenu').prop('disabled', true);

        $.ajax({
            url: '/api/metaforge/menus',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload)
        }).done(function () {
            MetaForgeUi.showAlert('Menu item saved successfully.', 'success', 2500);
            window.setTimeout(function () {
                window.location.href = '/Menu';
            }, 700);
        }).fail(function (xhr) {
            MetaForgeUi.showAlert(
                xhr.responseJSON?.title || xhr.responseJSON?.message || xhr.responseJSON?.error || 'Failed to save menu item.',
                'danger'
            );
        }).always(function () {
            $btn.prop('disabled', false);
        });
    }

    function deleteMenu(id, name) {
        const message = name
            ? 'Delete menu item "' + name + '"?'
            : 'Delete this menu item?';

        MetaForgeUi.confirmDelete({
            title: 'Delete Menu Item',
            message: message,
            detail: 'Child menu items must be removed or reassigned before deleting a folder.'
        }).then(function (confirmed) {
            if (!confirmed) return;

            $.ajax({
                url: '/api/metaforge/menus/' + id,
                method: 'DELETE'
            }).done(function () {
                MetaForgeUi.showAlert('Menu item deleted successfully.', 'success', 3000);
                window.setTimeout(function () {
                    window.location.reload();
                }, 800);
            }).fail(function (xhr) {
                MetaForgeUi.showAlert(
                    xhr.responseJSON?.title || xhr.responseJSON?.message || xhr.responseJSON?.error || 'Failed to delete menu item.',
                    'danger'
                );
            });
        });
    }

    return { init };
})();

$(function () {
    MenuManagement.init();
});
