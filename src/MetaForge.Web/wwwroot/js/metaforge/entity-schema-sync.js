/**
 * Entity schema sync — compare configured form with EF Core entity and merge changes.
 */
const EntitySchemaSync = (function () {
    let modalInstance = null;
    let activePreview = null;
    let activeFormId = 0;
    let onApplied = null;

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function init(options) {
        onApplied = options?.onApplied || null;
        ensureModal();
        bindEvents();
    }

    function ensureModal() {
        if ($('#entitySchemaSyncModal').length) return;

        $('body').append(`
            <div class="modal fade" id="entitySchemaSyncModal" tabindex="-1" aria-labelledby="entitySchemaSyncModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="entitySchemaSyncModalLabel">
                                <i class="fa-solid fa-rotate me-2"></i>Sync from Entity
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="entitySchemaSyncContext" class="validation-rule-field-context mb-3"></div>
                            <div id="entitySchemaSyncLoading" class="text-center py-4 d-none">
                                <div class="spinner-border text-primary" role="status"></div>
                                <div class="mt-2 text-muted">Comparing form with entity schema…</div>
                            </div>
                            <div id="entitySchemaSyncEmpty" class="alert alert-success d-none">
                                <i class="fa-solid fa-circle-check me-2"></i>
                                Form is already in sync with the entity. No changes needed.
                            </div>
                            <div id="entitySchemaSyncPanel" class="d-none">
                                <div class="d-flex flex-wrap gap-2 mb-3">
                                    <button type="button" class="btn btn-sm btn-outline-primary" id="btnSchemaSyncSelectAll">
                                        Select all additions
                                    </button>
                                    <button type="button" class="btn btn-sm btn-outline-secondary" id="btnSchemaSyncClearAll">
                                        Clear selection
                                    </button>
                                </div>
                                <div class="table-responsive">
                                    <table class="table table-sm table-striped mb-0" id="entitySchemaSyncTable">
                                        <thead>
                                            <tr>
                                                <th style="width:40px"><input type="checkbox" id="schemaSyncCheckAll" title="Select all" /></th>
                                                <th>Type</th>
                                                <th>Target</th>
                                                <th>Name</th>
                                                <th>Current</th>
                                                <th>Proposed</th>
                                                <th>Description</th>
                                            </tr>
                                        </thead>
                                        <tbody></tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" id="btnApplySchemaSync" class="btn btn-primary d-none">
                                <i class="fa-solid fa-check me-1"></i>Apply Selected
                            </button>
                        </div>
                    </div>
                </div>
            </div>`);

        modalInstance = bootstrap.Modal.getOrCreateInstance(document.getElementById('entitySchemaSyncModal'));
    }

    function bindEvents() {
        $(document).on('click', '#btnSchemaSyncSelectAll', function () {
            $('#entitySchemaSyncTable tbody input[type="checkbox"]').each(function () {
                const changeType = $(this).data('change-type');
                if (changeType === 'Add') $(this).prop('checked', true);
            });
            $('#schemaSyncCheckAll').prop('checked', false);
        });

        $(document).on('click', '#btnSchemaSyncClearAll', function () {
            $('#entitySchemaSyncTable tbody input[type="checkbox"]').prop('checked', false);
            $('#schemaSyncCheckAll').prop('checked', false);
        });

        $(document).on('change', '#schemaSyncCheckAll', function () {
            const checked = $(this).is(':checked');
            $('#entitySchemaSyncTable tbody input[type="checkbox"]').prop('checked', checked);
        });

        $(document).on('click', '#btnApplySchemaSync', applySelected);
    }

    function open(formId, contextLabel) {
        if (!formId || formId <= 0) {
            notify('Save the form first before syncing from the entity.', 'warning');
            return;
        }

        activeFormId = formId;
        activePreview = null;

        $('#entitySchemaSyncContext').html(
            `Comparing <strong>${esc(contextLabel || 'form')}</strong> with the latest EF Core entity schema.`
        );
        $('#entitySchemaSyncLoading').removeClass('d-none');
        $('#entitySchemaSyncEmpty').addClass('d-none');
        $('#entitySchemaSyncPanel').addClass('d-none');
        $('#btnApplySchemaSync').addClass('d-none');
        $('#entitySchemaSyncTable tbody').empty();
        modalInstance.show();

        $.getJSON(`/api/metaforge/formconfig/sync-preview/${formId}`)
            .done(function (preview) {
                activePreview = preview;
                renderPreview(preview);
            })
            .fail(function (xhr) {
                modalInstance.hide();
                notify('Failed to load schema sync preview: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger');
            })
            .always(function () {
                $('#entitySchemaSyncLoading').addClass('d-none');
            });
    }

    function renderPreview(preview) {
        const changes = preview.Changes ?? preview.changes ?? [];

        if (!changes.length) {
            $('#entitySchemaSyncEmpty').removeClass('d-none');
            return;
        }

        const $tbody = $('#entitySchemaSyncTable tbody').empty();

        changes.forEach(function (change) {
            const key = change.Key ?? change.key;
            const changeType = change.ChangeType ?? change.changeType;
            const target = change.Target ?? change.target;
            const name = change.Name ?? change.name;
            const description = change.Description ?? change.description ?? '';
            const current = change.CurrentSummary ?? change.currentSummary ?? '—';
            const proposed = change.ProposedSummary ?? change.proposedSummary ?? '—';
            const selected = change.SelectedByDefault ?? change.selectedByDefault ?? false;
            const badgeClass = changeType === 'Add' ? 'text-bg-success' : changeType === 'Remove' ? 'text-bg-danger' : 'text-bg-warning';

            $tbody.append(`
                <tr>
                    <td><input type="checkbox" class="form-check-input schema-sync-check" data-key="${escAttr(key)}" data-change-type="${escAttr(changeType)}" ${selected ? 'checked' : ''} /></td>
                    <td><span class="badge ${badgeClass}">${esc(changeType)}</span></td>
                    <td>${esc(target)}</td>
                    <td><code>${esc(name)}</code></td>
                    <td class="small">${esc(current)}</td>
                    <td class="small">${esc(proposed)}</td>
                    <td class="small">${esc(description)}</td>
                </tr>`);
        });

        $('#entitySchemaSyncPanel').removeClass('d-none');
        $('#btnApplySchemaSync').removeClass('d-none');
    }

    function applySelected() {
        if (!activePreview || !activeFormId) return;

        const keys = [];
        $('#entitySchemaSyncTable tbody .schema-sync-check:checked').each(function () {
            const key = $(this).data('key');
            if (key) keys.push(key);
        });

        if (!keys.length) {
            notify('Select at least one change to apply.', 'warning');
            return;
        }

        const $btn = $('#btnApplySchemaSync').prop('disabled', true);

        $.ajax({
            url: `/api/metaforge/formconfig/sync/${activeFormId}`,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ AcceptedKeys: keys })
        }).done(function (result) {
            modalInstance.hide();
            notify(`Applied ${keys.length} schema change(s) successfully.`, 'success');
            if (typeof onApplied === 'function') {
                onApplied(result);
            }
        }).fail(function (xhr) {
            notify('Schema sync failed: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger');
        }).always(function () {
            $btn.prop('disabled', false);
        });
    }

    function esc(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function escAttr(value) {
        return esc(value).replace(/"/g, '&quot;');
    }

    return { init, open };
})();
