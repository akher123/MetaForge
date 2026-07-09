/**
 * Form Builder health check dashboard.
 */
const FormHealthCheck = (function () {
    let healthTable = null;

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function init() {
        initTable();
        bindEvents();

        if (typeof EntitySchemaSync !== 'undefined') {
            EntitySchemaSync.init({
                onApplied: function () {
                    refreshReport();
                }
            });
        }
    }

    function initTable() {
        if (!$('#healthTable').length) return;

        healthTable = $('#healthTable').DataTable({
            pageLength: 25,
            order: [[4, 'asc'], [5, 'desc']],
            processing: false,
            metaforgeProgress: false,
            columnDefs: [
                { orderable: false, targets: [0, 6] },
                {
                    targets: 4,
                    render: function (data) {
                        return $(data).text() || data;
                    }
                }
            ],
            language: { search: 'Filter forms:' }
        });
    }

    function bindEvents() {
        $(document).on('click', '#btnRefreshHealth', refreshReport);
        $(document).on('click', '#btnSyncPermissions', syncPermissions);
        $(document).on('click', '.health-toggle', toggleDetails);
        $(document).on('click', '.btn-health-sync', openSchemaSync);
    }

    function toggleDetails() {
        const target = $(this).data('target');
        const $details = $(target);
        const $icon = $(this).find('i');

        $details.toggleClass('d-none');
        $icon.toggleClass('fa-chevron-right fa-chevron-down');
    }

    function openSchemaSync() {
        const formId = Number($(this).data('form-id'));
        const formName = $(this).data('form-name') || 'form';

        if (!formId || formId <= 0) {
            notify('Form id is missing.', 'warning');
            return;
        }

        if (typeof EntitySchemaSync !== 'undefined') {
            EntitySchemaSync.open(formId, formName, { cascade: true });
        }
    }

    function syncPermissions() {
        const $btn = $('#btnSyncPermissions').prop('disabled', true);

        $.post('/api/metaforge/security/permissions/sync')
            .done(function (result) {
                const added = result?.added ?? result?.Added ?? 0;
                notify(`Permission sync completed (${added} added).`, 'success');
                refreshReport();
            })
            .fail(function (xhr) {
                notify('Permission sync failed: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger');
            })
            .always(function () {
                $btn.prop('disabled', false);
            });
    }

    function refreshReport() {
        const $btn = $('#btnRefreshHealth').prop('disabled', true);

        $.getJSON('/api/metaforge/formconfig/health')
            .done(function () {
                window.location.reload();
            })
            .fail(function (xhr) {
                notify('Failed to refresh health report: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger');
            })
            .always(function () {
                $btn.prop('disabled', false);
            });
    }

    return { init };
})();

$(function () {
    FormHealthCheck.init();
});
