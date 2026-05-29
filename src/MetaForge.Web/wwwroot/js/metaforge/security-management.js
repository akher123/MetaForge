/**
 * Security Management helpers — role form permission matrix.
 */
const SecurityManagement = (function () {
    function setCheckboxState(el, total, checked) {
        el.checked = checked === total && total > 0;
        el.indeterminate = checked > 0 && checked < total;
    }

    function updateGroupCheckboxState() {
        $('.perm-group-check').each(function () {
            const group = $(this).data('group');
            const $items = $(`.perm-check[data-group="${group}"]`);
            const total = $items.length;
            const checked = $items.filter(':checked').length;
            setCheckboxState(this, total, checked);
            $(`.perm-count[data-group="${group}"]`).text(checked);
        });
    }

    function updateCategoryCheckboxState() {
        $('.perm-category-check').each(function () {
            const category = $(this).data('category');
            const $items = $(`.perm-check[data-category="${category}"]`);
            const total = $items.length;
            const checked = $items.filter(':checked').length;
            setCheckboxState(this, total, checked);
            $(`.perm-count[data-category="${category}"]`).text(checked);
        });
    }

    function updateColumnCheckboxState() {
        $('.perm-col-check').each(function () {
            const category = $(this).data('category');
            const action = $(this).data('action');
            const $items = $(`.perm-check[data-category="${category}"][data-action="${action}"]`);
            const total = $items.length;
            const checked = $items.filter(':checked').length;
            setCheckboxState(this, total, checked);
        });
    }

    function refreshMatrixState() {
        updateGroupCheckboxState();
        updateCategoryCheckboxState();
        updateColumnCheckboxState();
    }

    function setChevron($toggle, expanded) {
        const $icon = $toggle.find('.matrix-chevron, .permission-tree-chevron');
        $icon.removeClass('fa-chevron-down fa-chevron-right');
        $icon.addClass(expanded ? 'fa-chevron-down' : 'fa-chevron-right');
    }

    function bindMatrixToggles() {
        $('#permissionMatrix').on('show.bs.collapse', function (e) {
            const targetId = $(e.target).attr('id');
            $(`[data-bs-target="#${targetId}"]`).each(function () {
                setChevron($(this), true);
            });
        });

        $('#permissionMatrix').on('hide.bs.collapse', function (e) {
            const targetId = $(e.target).attr('id');
            $(`[data-bs-target="#${targetId}"]`).each(function () {
                setChevron($(this), false);
            });
        });
    }

    function bindPermissionTreeToggles() {
        $('#permissionTree').on('show.bs.collapse', function (e) {
            if (!$(e.target).closest('#permissionTree').length) {
                return;
            }

            const targetId = $(e.target).attr('id');
            $(`#permissionTree [data-bs-target="#${targetId}"]`).each(function () {
                setChevron($(this), true);
            });
        });

        $('#permissionTree').on('hide.bs.collapse', function (e) {
            if (!$(e.target).closest('#permissionTree').length) {
                return;
            }

            const targetId = $(e.target).attr('id');
            $(`#permissionTree [data-bs-target="#${targetId}"]`).each(function () {
                setChevron($(this), false);
            });
        });
    }

    function initPermissionsPage() {
        if (!$('#permissionsApp').length) {
            return;
        }

        bindPermissionTreeToggles();

        $('#btnPermExpandAll').on('click', function () {
            $('#permissionTree .collapse').addClass('show');
            $('#permissionTree .permission-tree-toggle').each(function () {
                setChevron($(this), true);
            });
        });

        $('#btnPermCollapseAll').on('click', function () {
            $('#permissionTree .collapse').removeClass('show');
            $('#permissionTree .permission-tree-toggle').each(function () {
                setChevron($(this), false);
            });
        });

        $('#btnSyncPermissions').on('click', function () {
            const $btn = $(this).prop('disabled', true);
            $.post('/api/metaforge/security/permissions/sync')
                .done(function (r) {
                    MetaForgeUi.showAlert(r.message || 'Permissions synced.', 'success', 3000);
                    window.setTimeout(function () { location.reload(); }, 900);
                })
                .fail(function (xhr) {
                    MetaForgeUi.showAlert(xhr.responseJSON?.error ?? 'Sync failed.', 'danger');
                })
                .always(function () { $btn.prop('disabled', false); });
        });
    }

    function initUserForm() {
        $('#userForm').on('submit', function (e) {
            e.preventDefault();
            const payload = {
                Id: parseInt($('#userId').val(), 10) || 0,
                UserName: $('#userName').val()?.trim(),
                Email: $('#email').val()?.trim(),
                Password: $('#password').val() || null,
                IsActive: $('#isActive').is(':checked'),
                RoleIds: $('.role-check:checked').map(function () {
                    return parseInt($(this).val(), 10);
                }).get()
            };

            $.ajax({
                url: '/api/metaforge/security/users',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload)
            }).done(function () {
                MetaForgeUi.showAlert('User saved successfully.', 'success', 3000);
                window.setTimeout(function () { window.location = '/Security/Users'; }, 800);
            }).fail(function (xhr) {
                MetaForgeUi.showAlert(xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? 'Save failed.', 'danger');
            });
        });
    }

    function initRoleForm() {
        bindMatrixToggles();
        refreshMatrixState();

        $('#btnExpandAll').on('click', () => {
            $('#permissionMatrix .collapse').addClass('show');
            $('#permissionMatrix .permission-matrix-toggle').each(function () {
                setChevron($(this), true);
            });
        });

        $('#btnCollapseAll').on('click', () => {
            $('#permissionMatrix .collapse').removeClass('show');
            $('#permissionMatrix .permission-matrix-toggle').each(function () {
                setChevron($(this), false);
            });
        });

        $('#btnSelectAll').on('click', () => {
            $('.perm-check').prop('checked', true);
            refreshMatrixState();
        });

        $('#btnClearAll').on('click', () => {
            $('.perm-check').prop('checked', false);
            refreshMatrixState();
        });

        $('.perm-category-check').on('change', function () {
            const category = $(this).data('category');
            const checked = $(this).is(':checked');
            $(`.perm-check[data-category="${category}"]`).prop('checked', checked);
            refreshMatrixState();
        });

        $('.perm-group-check').on('change', function () {
            const group = $(this).data('group');
            const checked = $(this).is(':checked');
            $(`.perm-check[data-group="${group}"]`).prop('checked', checked);
            refreshMatrixState();
        });

        $('.perm-col-check').on('change', function () {
            const category = $(this).data('category');
            const action = $(this).data('action');
            const checked = $(this).is(':checked');
            $(`.perm-check[data-category="${category}"][data-action="${action}"]`).prop('checked', checked);
            refreshMatrixState();
        });

        $('.perm-check').on('change', refreshMatrixState);

        $('#btnSaveRole').on('click', function () {
            const permissionIds = $('.perm-check:checked').map(function () {
                return parseInt($(this).val(), 10);
            }).get();

            const payload = {
                Id: parseInt($('#roleId').val(), 10) || 0,
                Name: $('#roleName').val(),
                Description: $('#roleDescription').val() || null,
                PermissionIds: permissionIds
            };

            $.ajax({
                url: '/api/metaforge/security/roles',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload)
            }).done(function () {
                MetaForgeUi.showAlert('Role saved successfully.', 'success', 3000);
                window.setTimeout(function () { window.location = '/Security/Roles'; }, 800);
            }).fail(function (xhr) {
                MetaForgeUi.showAlert(xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? 'Save failed.', 'danger');
            });
        });
    }

    return { initRoleForm, initUserForm, initPermissionsPage };
})();
