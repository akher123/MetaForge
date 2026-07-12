/**
 * Audit Log Explorer — server-side DataTables with detail modal.
 */
const AuditLog = (function () {
    let table;
    let detailModal;

    function actionBadge(action) {
        const normalized = (action || '').toLowerCase();
        let cls = 'bg-secondary';
        if (normalized === 'insert') cls = 'bg-success';
        else if (normalized === 'update') cls = 'bg-primary';
        else if (normalized === 'delete') cls = 'bg-danger';
        else if (normalized === 'savemasterdetail') cls = 'bg-info text-dark';

        return `<span class="badge ${cls}">${escapeHtml(action || '')}</span>`;
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function formatTimestamp(value) {
        if (!value) return '—';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return escapeHtml(value);
        return escapeHtml(date.toLocaleString());
    }

    function buildQuery(data) {
        const pageSize = data.length > 0 ? data.length : 25;
        const page = Math.floor(data.start / pageSize) + 1;

        const params = new URLSearchParams();
        params.set('page', String(page));
        params.set('pageSize', String(pageSize));

        const entity = $('#filterEntity').val();
        const action = $('#filterAction').val();
        const user = $('#filterUser').val()?.trim();
        const recordId = $('#filterRecordId').val()?.trim();
        const from = $('#filterFrom').val();
        const to = $('#filterTo').val();
        const search = data.search?.value?.trim();

        if (entity) params.set('entityName', entity);
        if (action) params.set('action', action);
        if (user) params.set('userName', user);
        if (recordId) params.set('recordId', recordId);
        if (from) params.set('from', from);
        if (to) params.set('to', to + 'T23:59:59');
        if (search) params.set('search', search);

        return params.toString();
    }

    function initTable() {
        table = $('#auditLogTable').DataTable({
            processing: true,
            serverSide: true,
            searching: true,
            scrollX: true,
            autoWidth: false,
            ajax: function (data, callback) {
                $.ajax({
                    url: '/api/metaforge/audit?' + buildQuery(data),
                    method: 'GET',
                    metaforgeProgress: false,
                    success: function (response) {
                        const items = response.items ?? response.Items ?? [];
                        callback({
                            draw: data.draw,
                            recordsTotal: response.totalCount ?? response.TotalCount ?? 0,
                            recordsFiltered: response.totalCount ?? response.TotalCount ?? 0,
                            data: items
                        });
                    },
                    error: function (xhr) {
                        if (xhr.status === 403) {
                            MetaForgeUi?.showAlert?.('You do not have permission to view audit logs.', 'danger');
                        } else if (xhr.status === 401) {
                            MetaForgeUi?.showAlert?.('Your session has expired. Please sign in again.', 'warning');
                        }
                        callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
                    }
                });
            },
            columns: [
                {
                    data: 'timestamp',
                    render: function (data, type, row) {
                        return formatTimestamp(data ?? row.Timestamp);
                    }
                },
                {
                    data: 'entityName',
                    render: function (data, type, row) {
                        return escapeHtml(data ?? row.EntityName ?? '');
                    }
                },
                {
                    data: 'recordId',
                    render: function (data, type, row) {
                        return `<code class="small">${escapeHtml(data ?? row.RecordId ?? '')}</code>`;
                    }
                },
                {
                    data: 'action',
                    render: function (data, type, row) {
                        return actionBadge(data ?? row.Action);
                    }
                },
                {
                    data: 'userName',
                    render: function (data, type, row) {
                        return escapeHtml(data ?? row.UserName ?? 'system');
                    }
                },
                {
                    data: 'summary',
                    render: function (data, type, row) {
                        return escapeHtml(data ?? row.Summary ?? '');
                    }
                },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    render: function (data, type, row) {
                        const id = row.id ?? row.Id;
                        return `<button type="button" class="btn btn-sm btn-outline-primary btn-view-audit" data-id="${id}" title="View details"><i class="fa-solid fa-eye"></i></button>`;
                    }
                }
            ],
            order: [[0, 'desc']],
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]]
        });
    }

    async function loadFilterOptions() {
        const [entities, actions] = await Promise.all([
            fetch('/api/metaforge/audit/entities').then(r => r.ok ? r.json() : []),
            fetch('/api/metaforge/audit/actions').then(r => r.ok ? r.json() : [])
        ]);

        const $entity = $('#filterEntity');
        entities.forEach(e => {
            const name = e.entityName ?? e.EntityName;
            const label = e.formName ?? e.FormName;
            const text = label ? `${label} (${name})` : name;
            $entity.append(`<option value="${escapeHtml(name)}">${escapeHtml(text)}</option>`);
        });

        const $action = $('#filterAction');
        actions.forEach(a => {
            $action.append(`<option value="${escapeHtml(a)}">${escapeHtml(a)}</option>`);
        });
    }

    function renderChanges(detail) {
        const changes = detail.changes ?? detail.Changes ?? [];
        const sections = detail.sections ?? detail.Sections ?? [];

        if (changes.length > 0) {
            let html = `<div class="table-responsive"><table class="table table-sm table-bordered mb-0">
                <thead class="table-light"><tr><th>Field</th><th>Before</th><th>After</th><th>Type</th></tr></thead><tbody>`;

            changes.forEach(c => {
                const type = c.changeType ?? c.ChangeType ?? 'Modified';
                const typeClass = type === 'Added' ? 'text-success' : type === 'Removed' ? 'text-danger' : 'text-primary';
                html += `<tr>
                    <td><strong>${escapeHtml(c.label ?? c.Label ?? c.field ?? c.Field)}</strong>
                        <div class="small text-muted">${escapeHtml(c.field ?? c.Field ?? '')}</div></td>
                    <td class="small">${escapeHtml(c.oldValue ?? c.OldValue ?? '—')}</td>
                    <td class="small">${escapeHtml(c.newValue ?? c.NewValue ?? '—')}</td>
                    <td class="small ${typeClass}">${escapeHtml(type)}</td>
                </tr>`;
            });

            html += '</tbody></table></div>';
            return html;
        }

        if (sections.length > 0) {
            let html = '';
            sections.forEach(s => {
                html += `<div class="mb-3">
                    <h6 class="fw-semibold">${escapeHtml(s.name ?? s.Name)}</h6>
                    <pre class="audit-json-block">${escapeHtml(s.content ?? s.Content ?? '')}</pre>
                </div>`;
            });
            return html;
        }

        const action = detail.action ?? detail.Action ?? '';
        if (action.toLowerCase() === 'insert') {
            return '<p class="text-muted mb-0">New record created. See Raw JSON tab for the full payload.</p>';
        }
        if (action.toLowerCase() === 'delete') {
            return '<p class="text-muted mb-0">Record deleted. See Raw JSON tab for the removed data.</p>';
        }

        return '<p class="text-muted mb-0">No field-level changes detected.</p>';
    }

    function renderTimeline(detail) {
        const timeline = detail.timeline ?? detail.Timeline ?? [];
        if (timeline.length === 0) {
            return '<p class="text-muted mb-0">No related history for this record.</p>';
        }

        let html = '<div class="list-group list-group-flush">';
        timeline.forEach(item => {
            const id = item.id ?? item.Id;
            const isCurrent = id === (detail.id ?? detail.Id);
            html += `<button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-start btn-timeline-entry ${isCurrent ? 'active' : ''}" data-id="${id}">
                <div>
                    <div class="fw-semibold">${actionBadge(item.action ?? item.Action)} <span class="ms-2">${escapeHtml(item.summary ?? item.Summary ?? '')}</span></div>
                    <div class="small ${isCurrent ? '' : 'text-muted'}">${formatTimestamp(item.timestamp ?? item.Timestamp)} · ${escapeHtml(item.userName ?? item.UserName ?? 'system')}</div>
                </div>
                ${isCurrent ? '<span class="badge bg-light text-dark">Current</span>' : '<i class="fa-solid fa-chevron-right text-muted"></i>'}
            </button>`;
        });
        html += '</div>';
        return html;
    }

    async function showDetail(id) {
        const res = await fetch(`/api/metaforge/audit/${id}`);
        if (!res.ok) {
            MetaForgeUi?.showAlert?.('Failed to load audit detail.', 'danger');
            return;
        }

        const detail = await res.json();
        const entity = detail.entityName ?? detail.EntityName;
        const recordId = detail.recordId ?? detail.RecordId;
        const action = detail.action ?? detail.Action;
        const user = detail.userName ?? detail.UserName ?? 'system';
        const summary = detail.summary ?? detail.Summary ?? '';

        $('#auditDetailModalLabel').text(`${entity} #${recordId}`);
        $('#auditDetailSubtitle').text(`${action} by ${user} · ${summary}`);

        $('#auditChangesContent').html(renderChanges(detail));
        $('#auditOldJson').text(detail.oldValueJson ?? detail.OldValueJson ?? '—');
        $('#auditNewJson').text(detail.newValueJson ?? detail.NewValueJson ?? '—');
        $('#auditTimelineContent').html(renderTimeline(detail));

        const modalEl = document.getElementById('auditDetailModal');
        if (!detailModal) detailModal = new bootstrap.Modal(modalEl);
        detailModal.show();
    }

    function bindEvents() {
        $('#auditLogTable').on('click', '.btn-view-audit', function () {
            const id = $(this).data('id');
            if (id) showDetail(id);
        });

        $('#auditTimelineContent').on('click', '.btn-timeline-entry', function () {
            const id = $(this).data('id');
            if (id) showDetail(id);
        });

        $('#filterEntity, #filterAction').on('change', () => table?.ajax.reload());
        $('#filterUser, #filterRecordId').on('keydown', function (e) {
            if (e.key === 'Enter') table?.ajax.reload();
        });
        $('#filterUser, #filterRecordId').on('blur', () => table?.ajax.reload());
        $('#filterFrom, #filterTo').on('change', () => table?.ajax.reload());

        $('#btnResetAuditFilters').on('click', function () {
            $('#filterEntity').val('');
            $('#filterAction').val('');
            $('#filterUser').val('');
            $('#filterRecordId').val('');
            $('#filterFrom').val('');
            $('#filterTo').val('');
            table?.search('').draw();
            table?.ajax.reload();
        });

        const params = new URLSearchParams(window.location.search);
        const entity = params.get('entity');
        const recordId = params.get('recordId');
        if (entity) $('#filterEntity').val(entity);
        if (recordId) $('#filterRecordId').val(recordId);
    }

    async function init() {
        if (!$('#auditLogTable').length) return;
        await loadFilterOptions();
        initTable();
        bindEvents();

        const params = new URLSearchParams(window.location.search);
        if (params.get('entity') || params.get('recordId')) {
            table?.ajax.reload();
        }
    }

    return { init };
})();

$(function () {
    AuditLog.init();
});
