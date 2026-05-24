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
        if ($('#menuTable').length) {
            $('#menuTable').DataTable({
                pageLength: 25,
                ordering: false,
                processing: false,
                metaforgeProgress: false,
                columnDefs: [{ orderable: false, targets: -1 }],
                language: { search: 'Filter menu:' }
            });
        }

        $('.btn-delete-menu').on('click', function () {
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
