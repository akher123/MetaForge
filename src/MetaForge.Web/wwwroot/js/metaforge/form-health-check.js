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
        if (!healthTable) return;

        const $tr = $(this).closest('tr');
        const row = healthTable.row($tr);
        const formId = $tr.data('form-id');
        const $icon = $(this).find('i');

        if (row.child.isShown()) {
            row.child.hide();
            $tr.removeClass('health-row-expanded');
            $icon.removeClass('fa-chevron-down').addClass('fa-chevron-right');
            return;
        }

        const template = document.getElementById('health-issues-' + formId);
        const html = template
            ? template.innerHTML
            : '<div class="health-details-panel"><div class="health-issue-item text-muted">No issue details.</div></div>';

        row.child($('<div class="health-details-child">').html(html)).show();
        $tr.addClass('health-row-expanded');
        $icon.removeClass('fa-chevron-right').addClass('fa-chevron-down');
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
