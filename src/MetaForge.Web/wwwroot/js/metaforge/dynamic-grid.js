/**
 * Dynamic Grid Engine - DataTables with server-side paging and configurable actions.
 */
const DynamicGrid = (function () {
    let table, gridDef, entityName, $table, permissions = {}, hasMasterDetail = false;

    function getColumns(def) {
        return def?.Columns ?? def?.columns ?? [];
    }

    function getActions(def) {
        return def?.Actions ?? def?.actions ?? [];
    }

    function getEntity(def, $el) {
        return def?.Entity ?? def?.entity ?? $el.data('entity') ?? '';
    }

    function getFormCode(def) {
        return def?.FormCode ?? def?.formCode ?? def?.moduleCode ?? '';
    }

    function getFormName(def) {
        return def?.FormName ?? def?.formName ?? 'Record';
    }

    function getRowId(row) {
        return row?.Id ?? row?.id ?? null;
    }

    function perm() {
        return MetaForgePermissions.createApi(permissions);
    }

    function canView() {
        return perm().canView();
    }

    function canEdit() {
        return perm().canEdit();
    }

    function canDelete() {
        return perm().canDelete();
    }

    function getRowActions() {
        return getActions(gridDef).filter(a => (a.Placement ?? a.placement ?? 'Row') === 'Row');
    }

    function getToolbarActions() {
        return getActions(gridDef).filter(a => (a.Placement ?? a.placement) === 'Toolbar');
    }

    function hasBuiltInActions() {
        return canEdit() || canDelete() || (hasMasterDetail && (canView() || canEdit()));
    }

    function hasActions() {
        return hasBuiltInActions() || getRowActions().length > 0;
    }

    function init(selector, definition, perms, masterDetail) {
        gridDef = definition || {};
        permissions = perms || {};
        hasMasterDetail = masterDetail === true;
        $table = $(selector);
        entityName = getEntity(gridDef, $table);
        const columns = getColumns(gridDef);

        renderToolbarActions();
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

    function renderToolbarActions() {
        const $container = $('#moduleGridToolbarActions');
        if (!$container.length) return;

        const actions = getToolbarActions();
        $container.empty();

        actions.forEach(action => {
            const code = action.Code ?? action.code;
            const label = action.Label ?? action.label ?? code;
            const style = action.ButtonStyle ?? action.buttonStyle ?? 'outline-primary';
            const icon = action.Icon ?? action.icon;
            const iconHtml = icon ? `<i class="fa-solid fa-${icon} me-1"></i>` : '';

            $container.append(
                `<button type="button" class="btn btn-${style} btn-grid-toolbar-action" data-action-code="${escAttr(code)}" title="${escAttr(label)}" aria-label="${escAttr(label)}">${iconHtml}${escHtml(label)}</button>`
            );
        });
    }

    function bindActionHandlers() {
        if ((canEdit() || canView()) && !hasMasterDetail) {
            $table.off('click', '.btn-edit, .btn-view').on('click', '.btn-edit, .btn-view', function (e) {
                e.preventDefault();
                const id = $(this).attr('data-id');
                if (!id) {
                    alert('Cannot open record: Id is missing.');
                    return;
                }
                const readOnly = !canEdit();
                DynamicForm.load(id).then(() => {
                    $('#recordFormModalTitle').text(
                        readOnly ? `View ${getFormName(gridDef)}` : `Edit ${getFormName(gridDef)}`);
                    $('#dynamicForm :input').prop('disabled', readOnly);
                    $('#btnSave').toggleClass('d-none', readOnly);
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

                const localeStrings = (window.__METAFORGE_LOCALE__ && window.__METAFORGE_LOCALE__.strings) || {};
                MetaForgeUi.confirmDelete({
                    title: hasMasterDetail ? 'Delete Document' : 'Delete Record',
                    message: hasMasterDetail
                        ? 'Delete this document and all its line items?'
                        : 'Delete this record?',
                    detail: localeStrings.confirmDeleteDetail || 'This action cannot be undone.'
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

        $table.off('click', '.btn-grid-custom-action').on('click', '.btn-grid-custom-action', function (e) {
            e.preventDefault();
            const code = $(this).data('action-code');
            const action = getRowActions().find(a => (a.Code ?? a.code) === code);
            if (!action) return;

            const rowIndex = $(this).data('row-index');
            const rowData = table.row(rowIndex).data();
            executeGridAction(action, rowData);
        });

        $(document).off('click', '#moduleGridToolbarActions .btn-grid-toolbar-action')
            .on('click', '#moduleGridToolbarActions .btn-grid-toolbar-action', function (e) {
                e.preventDefault();
                const code = $(this).data('action-code');
                const action = getToolbarActions().find(a => (a.Code ?? a.code) === code);
                if (!action) return;
                executeGridAction(action, null);
            });
    }

    function buildActionContext(row) {
        const id = row ? getRowId(row) : null;
        const context = {
            id,
            formCode: getFormCode(gridDef),
            entity: entityName
        };

        if (row) {
            Object.keys(row).forEach(key => {
                context[key] = row[key];
            });
        }

        return context;
    }

    function resolveTemplate(template, context) {
        return String(template ?? '').replace(/\{(\w+)\}/g, (_, key) => {
            const value = context[key];
            return value == null ? '' : String(value);
        });
    }

    function executeGridAction(action, row) {
        const context = buildActionContext(row);
        const label = action.Label ?? action.label ?? action.Code ?? action.code ?? 'Action';

        const run = () => {
            const handlerType = action.HandlerType ?? action.handlerType ?? 'Api';
            const target = resolveTemplate(action.HandlerTarget ?? action.handlerTarget, context);

            if (!target) {
                MetaForgeUi.showAlert('Action target is not configured.', 'warning');
                return;
            }

            if (handlerType === 'Redirect') {
                window.location.href = target;
                return;
            }

            if (handlerType === 'Script') {
                const handler = window.MetaForgeGridActionHandlers?.[target];
                if (typeof handler !== 'function') {
                    MetaForgeUi.showAlert(`Custom handler "${target}" is not registered.`, 'warning');
                    return;
                }
                handler(context);
                return;
            }

            const formCode = getFormCode(gridDef);
            const actionCode = action.Code ?? action.code;
            const recordId = context.id;
            const executeUrl = recordId != null
                ? `/api/metaforge/grid/${encodeURIComponent(formCode)}/actions/${encodeURIComponent(actionCode)}/${encodeURIComponent(recordId)}`
                : `/api/metaforge/grid/${encodeURIComponent(formCode)}/actions/${encodeURIComponent(actionCode)}`;

            $.ajax({
                url: executeUrl,
                method: 'POST',
                metaforgeProgress: true
            })
                .done(function () {
                    MetaForgeUi.showAlert(`${label} completed successfully.`, 'success', 3000);
                    if (table) table.ajax.reload(null, false);
                })
                .fail(function (xhr) {
                    MetaForgeUi.showAlert(
                        xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? xhr.responseText ?? xhr.statusText ?? `${label} failed.`,
                        'danger'
                    );
                });
        };

        const confirmMessage = action.ConfirmMessage ?? action.confirmMessage;
        if (confirmMessage) {
            MetaForgeUi.confirmDelete({
                title: label,
                message: confirmMessage,
                detail: ''
            }).then(confirmed => {
                if (confirmed) run();
            });
            return;
        }

        run();
    }

    function buildColumns(columns) {
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
            const controlType = col.ControlType ?? col.controlType;
            const displayFormat = col.DisplayFormat ?? col.displayFormat;
            const hasTemporalFormat = typeof MetaForgeGridDisplayFormat !== 'undefined'
                && (MetaForgeGridDisplayFormat.isTemporalControlType(controlType)
                    || (displayFormat && String(displayFormat).trim()));

            return {
                data: prop,
                title: col.Label ?? col.label,
                orderable: col.IsSortable ?? col.isSortable ?? true,
                render: (data) => {
                    if (data == null || data === '') return '';

                    let text = data;
                    if (hasTemporalFormat && typeof MetaForgeGridDisplayFormat !== 'undefined') {
                        text = MetaForgeGridDisplayFormat.formatValue(data, displayFormat, controlType);
                    } else if (MetaForgeControlTypes.isRichText(controlType) && typeof MetaForgeGridDisplayFormat !== 'undefined') {
                        text = MetaForgeGridDisplayFormat.stripHtml(data);
                        return escapeHtml(String(text));
                    }

                    if (lookupEntity) {
                        return `<span class="grid-lookup-value">${escapeHtml(String(text))}</span>`;
                    }

                    return hasTemporalFormat ? escapeHtml(String(text)) : text;
                }
            };
        });

        if (hasActions()) {
            const customRowActions = getRowActions();
            cols.push({
                data: null,
                orderable: false,
                searchable: false,
                render: (data, type, row, meta) => {
                    const id = getRowId(row);
                    if (id == null && customRowActions.length === 0) return '<span class="text-muted">—</span>';

                    const buttons = [];
                    if (hasMasterDetail && (canEdit() || canView())) {
                        const label = canEdit() ? 'Edit' : 'View';
                        const icon = canEdit() ? MetaForgeIcons.edit : MetaForgeIcons.view;
                        const btnClass = canEdit() ? 'btn-edit btn-master-edit' : 'btn-view btn-master-edit';
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-action-edit ${btnClass}" data-id="${id}" title="${label}" aria-label="${label}">${icon}</button>`);
                    } else if (canEdit()) {
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-icon btn-action-edit btn-edit" data-id="${id}" title="Edit" aria-label="Edit">${MetaForgeIcons.edit}</button>`);
                    } else if (canView()) {
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-secondary btn-icon btn-action-view btn-view" data-id="${id}" title="View" aria-label="View">${MetaForgeIcons.view}</button>`);
                    }
                    if (canDelete()) {
                        buttons.push(`<button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-action-delete btn-delete" data-id="${id}" data-entity="${entity}" title="Delete" aria-label="Delete">${MetaForgeIcons.delete}</button>`);
                    }

                    customRowActions.forEach(action => {
                        buttons.push(renderCustomActionButton(action, meta.row));
                    });

                    return buttons.join(' ');
                }
            });
        }

        return cols;
    }

    function renderCustomActionButton(action, rowIndex) {
        const code = action.Code ?? action.code;
        const label = action.Label ?? action.label ?? code;
        const style = action.ButtonStyle ?? action.buttonStyle ?? 'outline-primary';
        const icon = action.Icon ?? action.icon;
        const iconHtml = icon ? MetaForgeIcons.icon(icon) : MetaForgeIcons.apply;

        return `<button type="button" class="btn btn-sm btn-${style} btn-icon btn-grid-custom-action" data-action-code="${escAttr(code)}" data-row-index="${rowIndex}" title="${escAttr(label)}" aria-label="${escAttr(label)}">${iconHtml}</button>`;
    }

    function escAttr(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function escHtml(value) {
        return escAttr(value);
    }

    function reload() {
        if (table) table.ajax.reload(null, false);
    }

    return { init, reload };
})();

/** Register custom Script-type grid action handlers: MetaForgeGridActionHandlers.myHandler = (ctx) => {} */
window.MetaForgeGridActionHandlers = window.MetaForgeGridActionHandlers || {};
