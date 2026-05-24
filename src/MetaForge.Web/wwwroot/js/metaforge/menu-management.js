/**
 * Menu management — create/edit/delete navigation entries.
 */
const MenuManagement = (function () {
    function init() {
        toggleTypeFields();
        $('#itemType').on('change', toggleTypeFields);

        $('#btnSaveMenu').on('click', saveMenu);
        $('.btn-delete-menu').on('click', function () {
            const id = $(this).data('id');
            const name = $(this).data('name');
            deleteMenu(id, name);
        });
    }

    function toggleTypeFields() {
        const type = $('#itemType').val();
        $('.module-fields').toggle(type === 'Form');
        $('.url-fields').toggle(type === 'Url');
    }

    function saveMenu() {
        const payload = {
            id: parseInt($('#menuId').val(), 10) || 0,
            parentId: $('#parentId').val() ? parseInt($('#parentId').val(), 10) : null,
            name: $('#menuName').val()?.trim(),
            icon: $('#menuIcon').val()?.trim() || null,
            itemType: $('#itemType').val(),
            moduleId: $('#moduleId').val() ? parseInt($('#moduleId').val(), 10) : null,
            action: $('#menuAction').val(),
            url: $('#menuUrl').val()?.trim() || null,
            displayOrder: parseInt($('#displayOrder').val(), 10) || 0,
            isActive: $('#isActive').is(':checked')
        };

        $.ajax({
            url: '/api/metaforge/menus',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function () {
                window.location.href = '/Menu';
            },
            error: function (xhr) {
                MetaForgeUi.showAlert(
                    xhr.responseJSON?.title || xhr.responseJSON?.message || 'Failed to save menu item.',
                    'danger'
                );
            }
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
                method: 'DELETE',
                success: function () {
                    MetaForgeUi.showAlert('Menu item deleted successfully.', 'success', 3000);
                    window.setTimeout(function () {
                        window.location.reload();
                    }, 800);
                },
                error: function (xhr) {
                    MetaForgeUi.showAlert(
                        xhr.responseJSON?.title || xhr.responseJSON?.message || 'Failed to delete menu item.',
                        'danger'
                    );
                }
            });
        });
    }

    return { init };
})();

$(function () {
    MenuManagement.init();
});
