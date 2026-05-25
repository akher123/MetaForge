/**
 * Dynamic Grid Engine - DataTables with server-side paging.
 */
const DynamicGrid = (function () {
    let table, gridDef, entityName, $table, permissions = {}, hasMasterDetail = false;

    function getColumns(def) {
        return def?.Columns ?? def?.columns ?? [];
    }

    function getEntity(def, $el) {
        return def?.Entity ?? def?.entity ?? $el.data('entity') ?? '';
    }

    function getFormCode(def) {
        return def?.FormCode ?? def?.moduleCode ?? '';
    }

    function getRowId(row) {
        return row?.Id ?? row?.id ?? null;
    }

    function canView() {
        return permissions?.CanView === true || permissions?.canView === true;
    }

    function canEdit() {
        return permissions?.CanEdit === true || permissions?.canEdit === true;
    }

    function canDelete() {
        return permissions?.CanDelete === true || permissions?.canDelete === true;
    }

    function hasActions() {
        return canEdit() || canDelete() || (hasMasterDetail && (canView() || canEdit()));
    }

    function init(selector, definition, perms, masterDetail) {
        gridDef = definition || {};
        permissions = perms || {};
        hasMasterDetail = masterDetail === true;
        $table = $(selector);
        entityName = getEntity(gridDef, $table);
        const columns = getColumns(gridDef);

        bindActionHandlers();

        table = $table.DataTable({
            processing: false,
            serverSide: true,
            scrollX: true,
            autoWidth: false,
            ajax: function (data, callback) {
                const pageSize = data.length > 0 ? data.length : 25;
                const page = Math.floor(data.start / pageSize) + 1;
                const sortColIndex = data.order?.length ? data.order[0].column : 0;
                const sortCol = columns[sortColIndex];

                const request = {
                    Entity: entityName,
                    Page: page,
                    PageSize: pageSize,
                    SearchTerm: data.search?.value || '',
                    SortColumn: sortCol?.PropertyName ?? sortCol?.propertyName ?? null,
                    SortDescending: data.order?.length ? data.order[0].dir === 'desc' : false
                };

                $.ajax({
                    url: '/api/metaforge/grid/data',
                    method: 'POST',
                    contentType: 'application/json',
                    metaforgeProgress: false,
                    data: JSON.stringify(request),
                    success: function (response) {
                        callback({
                            draw: data.draw,
                            recordsTotal: response.TotalCount ?? response.totalCount ?? 0,
                            recordsFiltered: response.TotalCount ?? response.totalCount ?? 0,
                            data: response.Items ?? response.items ?? []
                        });
                        if (typeof MetaForgeProgress !== 'undefined') {
                            MetaForgeProgress.finishPageLoad();
                        }
                    },
                    error: function (xhr) {
                        console.error('Grid load failed:', xhr.responseText || xhr.statusText);
                        if (xhr.status === 403) {
                            alert('You do not have permission to view this data.');
                        } else if (xhr.status === 401) {
                            alert('Your session has expired. Please sign in again.');
                        }
                        callback({
                            draw: data.draw,
                            recordsTotal: 0,
                            recordsFiltered: 0,
                            data: []
                        });
                        if (typeof MetaForgeProgress !== 'undefined') {
                            MetaForgeProgress.finishPageLoad();
                        }
                    }
                });
            },
            columns: buildColumns(columns),
            order: [[0, 'asc']],
            pageLength: 15,
            lengthMenu: [[10, 15, 25, 50], [10, 15, 25, 50]],
            drawCallback: function () {
                $table.closest('.dataTables_wrapper').find('.dataTables_processing').hide();
            }
        });
    }

    function bindActionHandlers() {
        if (canEdit() && !hasMasterDetail) {
            $table.off('click', '.btn-edit').on('click', '.btn-edit', function (e) {
                e.preventDefault();
                const id = $(this).attr('data-id');
                if (!id) {
                    alert('Cannot edit: record Id is missing.');
                    return;
                }
                DynamicForm.load(id).then(() => {
                    $('#recordFormModalTitle').text('Edit Record');
                    bootstrap.Modal.getOrCreateInstance(document.getElementById('recordFormModal')).show();
                }).fail(function (xhr) {
                    alert('Failed to load record: ' + (xhr.responseJSON?.error ?? xhr.statusText));
                });
            });
        }

        if (canDelete()) {
            $table.off('click', '.btn-delete').on('click', '.btn-delete', function (e) {
                e.preventDefault();
                const id = $(this).attr('data-id');
                const ent = $(this).attr('data-entity') || entityName;
                if (!id) {
                    MetaForgeUi.showAlert('Cannot delete: record Id is missing.', 'warning');
                    return;
                }

                MetaForgeUi.confirmDelete({
                    title: hasMasterDetail ? 'Delete Document' : 'Delete Record',
                    message: hasMasterDetail
                        ? 'Delete this document and all its line items?'
                        : 'Delete this record?',
                    detail: 'This action cannot be undone.'
                }).then(function (confirmed) {
                    if (!confirmed) return;

                    $.ajax({ url: `/api/metaforge/crud/${ent}/${id}`, method: 'DELETE' })
                        .done(function () {
                            MetaForgeUi.showAlert(
                                hasMasterDetail ? 'Document deleted successfully.' : 'Record deleted successfully.',
                                'success',
                                3000
                            );
                            table.ajax.reload(null, false);
                        })
                        .fail(function (xhr) {
                            MetaForgeUi.showAlert(
                                xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? xhr.responseText ?? xhr.statusText ?? 'Delete failed.',
                                'danger'
                            );
                        });
                });
            });
        }
    }

    function buildColumns(columns) {
        const module = getFormCode(gridDef);
        const entity = entityName;

        function escapeHtml(value) {
            return String(value ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }

        const cols = columns.map(col => {
            const prop = col.PropertyName ?? col.propertyName;
            const lookupEntity = col.LookupEntity ?? col.lookupEntity;
            return {
                data: prop,
                title: col.Label ?? col.label,
                orderable: col.IsSortable ?? col.isSortable ?? true,
                render: (data) => {
                    if (data == null || data === '') return '';
                    return lookupEntity ? `<span class="grid-lookup-value">${escapeHtml(String(data))}</span>` : data;
                }
            };
        });

        if (hasActions()) {
            cols.push({
                data: null,
                orderable: false,
                searchable: false,
                render: (data, type, row) => {
                    const id = getRowId(row);
                    if (id == null) return '<span class="text-muted">—</span>';

                    const buttons = [];
                    if (hasMasterDetail && (canEdit() || canView())) {
                        const label = canEdit() ? 'Edit' : 'View';
                        const icon = canEdit() ? MetaForgeIcons.edit : MetaForgeIcons.view;
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-action-edit btn-master-edit" data-id="${id}" title="${label}" aria-label="${label}">${icon}</button>`);
                    } else if (canEdit()) {
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-action-edit btn-edit" data-id="${id}" title="Edit" aria-label="Edit">${MetaForgeIcons.edit}</button>`);
                    }
                    if (canDelete()) {
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-action-delete btn-delete" data-id="${id}" data-entity="${entity}" title="Delete" aria-label="Delete">${MetaForgeIcons.delete}</button>`);
                    }
                    return buttons.join(' ');
                }
            });
        }

        return cols;
    }

    function reload() {
        if (table) table.ajax.reload(null, false);
    }

    return { init, reload };
})();
