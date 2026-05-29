/**
 * Master-Detail Engine - modal master form + inline detail line CRUD.
 */
const MasterDetail = (function () {
    let screen, moduleCode, detailRows = [], deletedDetailIds = [], permissions = {};
    let editState = MetaForgeDetailRows.createEditState();
    let config = { mode: 'page', onSaved: null, autoOpenMaster: false };
    let listModal = null;
    let masterModal = null;

    function canCreate() {
        return permissions?.CanCreate === true;
    }

    function canEdit() {
        return permissions?.CanEdit === true;
    }

    function canDelete() {
        return permissions?.CanDelete === true;
    }

    function canModifyDetails() {
        return canCreate() || canEdit();
    }

    function canSave() {
        return canCreate() || canEdit();
    }

    function getDetailFields() {
        const formFields = screen.DetailForm?.Fields ?? screen.detailForm?.fields;
        if (formFields?.length) {
            return formFields;
        }

        return (screen.DetailGrid?.Columns ?? screen.detailGrid?.columns ?? []).map(col => ({
            PropertyName: col.PropertyName ?? col.propertyName,
            Label: col.Label ?? col.label,
            ControlType: col.ControlType ?? col.controlType ?? inferControlType(col.PropertyName ?? col.propertyName),
            LookupEntity: col.LookupEntity ?? col.lookupEntity
                ?? ((col.PropertyName ?? col.propertyName)?.endsWith('Id')
                    ? (col.PropertyName ?? col.propertyName).replace(/Id$/, '')
                    : null),
            LookupParentField: col.LookupParentField ?? col.lookupParentField,
            LookupFilterField: col.LookupFilterField ?? col.lookupFilterField,
            IsRequired: false,
            IsReadOnly: false
        }));
    }

    function inferControlType(propertyName) {
        if (!propertyName) return 'TextBox';
        if (propertyName.endsWith('Id')) return 'Autocomplete';
        const lower = propertyName.toLowerCase();
        if (lower.includes('date')) return 'DateTime';
        if (lower.includes('quantity') || lower.includes('price') || lower.includes('amount') || lower.includes('total')) {
            return 'Number';
        }
        if (lower.includes('description') || lower.includes('notes')) return 'TextArea';
        return 'TextBox';
    }

    function init(screenData, module, perms, options) {
        config = { mode: 'page', onSaved: null, autoOpenMaster: false, ...(options || {}) };
        screen = screenData;
        moduleCode = module;
        permissions = perms || {};
        detailRows = screen.DetailData ? screen.DetailData.map(r => ({ ...r })) : [];
        deletedDetailIds = [];
        editState = MetaForgeDetailRows.createEditState();

        const moduleName = screen.MasterForm?.FormName ?? screen.masterForm?.moduleName ?? 'Record';
        $('#masterForm').attr('data-module', moduleCode).attr('data-entity', screen.MasterForm?.EntityName ?? '');

        DynamicForm.init('#masterForm', screen.MasterForm, { layoutClass: 'master-detail-form' });
        if (screen.MasterData) {
            DynamicForm.setDataWhenReady(screen.MasterData);
        } else {
            DynamicForm.refreshLookups();
        }

        if (!canModifyDetails()) {
            $('#masterForm :input').prop('disabled', true);
        }

        renderDetailHeader();
        renderDetailGrid();
        MetaForgeDetailRows.resolveDisplayLabels(detailRows, getDetailFields()).always(function () {
            renderDetailGrid();
        });
        updateSummary();
        updateMasterSummary();
        configureActionButtons();
        bindEvents();

        if (config.mode === 'split-modal' && config.autoOpenMaster && canSave()) {
            openMasterModal();
        }
    }

    function loadAndOpen(module, recordId, perms, options) {
        const url = recordId
            ? `/api/metaforge/masterdetail/${module}/${recordId}`
            : `/api/metaforge/masterdetail/${module}`;

        return $.getJSON(url).done(screenData => {
            init(screenData, module, perms, { mode: 'list-modal', ...(options || {}) });

            const moduleName = screenData.MasterForm?.FormName ?? 'Record';
            const title = recordId ? `Edit ${moduleName}` : `New ${moduleName}`;
            $('#masterDetailModalTitle').text(title);
            $('#detailSectionLabel').text(screenData.DetailForm?.FormName ?? screenData.DetailRelation?.ChildEntity ?? 'Line Items');

            listModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('masterDetailModal'));
            listModal.show();
        }).fail(function (xhr) {
            alert('Failed to load entry: ' + (xhr.responseJSON?.error ?? xhr.statusText));
        });
    }

    function configureActionButtons() {
        $('#btnAddDetail').toggleClass('d-none', !canCreate());
        $('#btnSaveAll').toggleClass('d-none', !canSave());
    }

    function renderDetailHeader() {
        const $head = $('#detailGridHead');
        if ($head.length === 0) return;

        const fields = getDetailFields().filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');
        $head.empty().append('<th style="width:48px">#</th>');
        fields.forEach(f => {
            const label = f.Label ?? f.label ?? f.PropertyName ?? f.propertyName;
            $head.append(`<th>${MetaForgeDetailRows.escapeHtml(label)}</th>`);
        });
        if (canEdit() || canDelete()) {
            $head.append('<th style="width:120px" class="text-center">Actions</th>');
        }

        const colspan = fields.length + ((canEdit() || canDelete()) ? 2 : 1);
        $('#detailSummaryRow td').attr('colspan', colspan);
    }

    function renderDetailGrid() {
        const $body = $('#detailGridBody');
        $body.empty();

        const fields = getDetailFields();
        const visibleFields = fields.filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');
        const actionCols = (canEdit() || canDelete()) ? 1 : 0;
        if (detailRows.length === 0) {
            const colspan = visibleFields.length + actionCols + 1;
            $body.append(`<tr class="detail-empty-row"><td colspan="${colspan}" class="text-center text-muted py-4">No line items yet. Click "Add Item" to begin.</td></tr>`);
            return;
        }

        detailRows.forEach((row, index) => {
            $body.append(buildDetailRow(row, index, fields));
        });

        initLookups($body);
        updateSummary();
    }

    function buildDetailRow(row, index, fields) {
        const editing = MetaForgeDetailRows.isEditing(editState, index);
        const visibleFields = fields.filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');
        const lineCell = `<td class="text-muted line-number">${index + 1}</td>`;
        const idValue = row.Id ?? row.id ?? '';
        const hiddenId = `<input type="hidden" class="detail-id" data-index="${index}" value="${MetaForgeDetailRows.escapeAttr(idValue)}" />`;
        const rowClass = editing ? 'detail-row-editing' : 'detail-row-view';

        const cols = visibleFields.map(field => {
            const name = field.PropertyName ?? field.propertyName;
            const val = row[name];
            if (editing && canModifyDetails() && !(field.IsReadOnly ?? field.isReadOnly)) {
                return `<td>${MetaForgeDetailRows.buildInlineControl(field, index, val, { readOnly: false })}</td>`;
            }
            return `<td>${MetaForgeDetailRows.buildDisplayCell(field, row)}</td>`;
        }).join('');

        const actionCell = MetaForgeDetailRows.buildActionCell(row, index, {
            canEdit: canEdit() && canModifyDetails(),
            canDelete: canDelete(),
            editing
        });

        return `<tr class="${rowClass}" data-index="${index}">${hiddenId}${lineCell}${cols}${actionCell}</tr>`;
    }

    function initLookups($container) {
        if (typeof MetaForgeLookups !== 'undefined') {
            MetaForgeLookups.initGridLookups($container, getDetailFields(), index => detailRows[index]);
            return;
        }

        $container.find('.lookup-select').each(function () {
            const $sel = $(this);
            const entity = $sel.data('lookup');
            const index = $sel.data('index');
            const field = $sel.data('field');
            const currentVal = detailRows[index]?.[field];

            $.getJSON(`/api/metaforge/lookups/${entity}`, items => {
                $sel.empty().append('<option value="">-- Select --</option>');
                items.forEach(i => {
                    const val = i.Value ?? i.value;
                    const text = i.Text ?? i.text;
                    $sel.append(`<option value="${val}">${escapeHtml(text)}</option>`);
                });
                if (currentVal != null && currentVal !== '') {
                    $sel.val(currentVal);
                }
            });
        });
    }

    function bindEvents() {
        $('#btnAddDetail').off('click').on('click', () => {
            detailRows.push({});
            MetaForgeDetailRows.enterEdit(editState, detailRows, detailRows.length - 1);
            renderDetailGrid();
        });

        $(document).off('click.masterDetail', '.btn-detail-row-edit').on('click.masterDetail', '.btn-detail-row-edit', function () {
            const index = $(this).data('index');
            MetaForgeDetailRows.enterEdit(editState, detailRows, index);
            renderDetailGrid();
        });

        $(document).off('click.masterDetail', '.btn-detail-row-done').on('click.masterDetail', '.btn-detail-row-done', function () {
            const index = $(this).data('index');
            syncRowFromDom(index);
            MetaForgeDetailRows.finishEdit(editState, index);
            renderDetailGrid();
        });

        $(document).off('click.masterDetail', '.btn-detail-row-cancel').on('click.masterDetail', '.btn-detail-row-cancel', function () {
            const index = $(this).data('index');
            const result = MetaForgeDetailRows.cancelEdit(editState, detailRows, index, getDetailFields());
            renderDetailGrid();
            if (result === 'removed') {
                updateSummary();
            }
        });

        $(document).off('click.masterDetail', '.btn-remove-detail').on('click.masterDetail', '.btn-remove-detail', function () {
            const index = $(this).data('index');
            const row = detailRows[index];
            const id = row?.Id ?? row?.id;
            const isPersisted = id != null && id !== '' && parseInt(id, 10) > 0;
            const lineNo = index + 1;
            const detailLabel = screen.DetailForm?.FormName ?? screen.detailForm?.formName ?? 'Line item';

            MetaForgeUi.confirmDelete({
                title: 'Remove Line Item',
                message: isPersisted
                    ? `Remove ${detailLabel.toLowerCase()} #${lineNo}?`
                    : `Remove unsaved ${detailLabel.toLowerCase()} #${lineNo}?`,
                detail: isPersisted
                    ? 'The line will be deleted when you save the document.'
                    : 'This line has not been saved yet.'
            }).then(function (confirmed) {
                if (!confirmed) return;

                if (isPersisted) {
                    deletedDetailIds.push(parseInt(id, 10));
                }
                detailRows.splice(index, 1);
                MetaForgeDetailRows.clearEditState(editState);
                renderDetailGrid();
            });
        });

        $(document).off('change.masterDetail input.masterDetail', '#detailGridBody .detail-input')
            .on('change.masterDetail input.masterDetail', '#detailGridBody .detail-input', function () {
                const index = $(this).data('index');
                syncRowFromDom(index);
                updateSummary();
            });

        $('#btnSaveAll').off('click').on('click', saveAll);
        $('#btnEditMaster').off('click').on('click', openMasterModal);
        $('#btnApplyMaster').off('click').on('click', applyMasterModal);
    }

    function openMasterModal() {
        masterModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('masterFormModal'));
        masterModal.show();
    }

    function applyMasterModal() {
        if (!validateMasterForm()) return;
        updateMasterSummary();
        if (masterModal) masterModal.hide();
    }

    function isEmptyRequiredValue(val, fieldName) {
        if (val == null || val === '') return true;
        if (fieldName && fieldName.endsWith('Id') && (val === 0 || val === '0')) return true;
        return false;
    }

    function validateMasterForm() {
        const fields = screen.MasterForm?.Fields ?? screen.masterForm?.fields ?? [];
        const errors = DynamicForm.validateRequiredFields($('#masterForm'), fields, DynamicForm.getData());
        return !errors;
    }

    function updateMasterSummary() {
        const $summary = $('#masterSummary');
        if ($summary.length === 0) return;

        const fields = screen.MasterForm?.Fields ?? screen.masterForm?.fields ?? [];
        const data = DynamicForm.getData();
        const parts = fields
            .filter(f => (f.IsVisible ?? f.isVisible) !== false && (f.ControlType ?? f.controlType) !== 'Hidden')
            .slice(0, 4)
            .map(f => {
                const name = f.PropertyName ?? f.propertyName;
                let val = data[name];
                if (val == null || val === '') return null;

                const controlType = f.ControlType ?? f.controlType ?? 'TextBox';
                if (controlType === 'Dropdown' || controlType === 'Autocomplete') {
                    const $sel = $(`#masterForm [name="${name}"], #masterForm select[data-field="${name}"]`).first();
                    if ($sel.length) {
                        const selectedText = $sel.find('option:selected').text()?.trim();
                        if (selectedText && selectedText !== '-- Select --') {
                            val = selectedText;
                        }
                    }
                }

                return `<span class="me-3"><strong>${escapeHtml(f.Label ?? f.label ?? name)}:</strong> ${escapeHtml(val)}</span>`;
            })
            .filter(Boolean);

        $summary.html(parts.length ? parts.join('') : '<span class="text-muted">No header entered yet.</span>');
    }

    function syncRowFromDom(index) {
        if (index == null || !detailRows[index]) return;

        getDetailFields().forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $input = $(`#detailGridBody [data-field="${name}"][data-index="${index}"]`);
            if ($input.length === 0) return;

            const controlType = field.ControlType ?? field.controlType ?? 'TextBox';
            if ($input.attr('type') === 'checkbox') {
                detailRows[index][name] = $input.is(':checked');
            } else {
                detailRows[index][name] = $input.val();
            }

            MetaForgeDetailRows.syncDisplayFromInput(detailRows[index], field, $input);

            if (controlType === 'Number') {
                const num = parseFloat(detailRows[index][name]);
                detailRows[index][name] = Number.isNaN(num) ? null : num;
            } else if ((controlType === 'Dropdown' || controlType === 'Autocomplete') && name.endsWith('Id')) {
                const num = parseInt(detailRows[index][name], 10);
                detailRows[index][name] = Number.isNaN(num) ? null : num;
            }
        });

        const idVal = $(`#detailGridBody .detail-id[data-index="${index}"]`).val();
        if (idVal) {
            detailRows[index].Id = parseInt(idVal, 10);
        }
    }

    function syncAllRowsFromDom() {
        [...editState.editing].forEach(index => syncRowFromDom(index));
    }

    function normalizeDetailRow(row) {
        const copy = { ...row };
        delete copy.__display;
        getDetailFields().forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const controlType = field.ControlType ?? field.controlType ?? 'TextBox';

            if (copy[name] === '' || copy[name] === undefined) {
                copy[name] = null;
                return;
            }

            if ((controlType === 'Dropdown' || controlType === 'Autocomplete') && name.endsWith('Id')) {
                const num = parseInt(copy[name], 10);
                copy[name] = Number.isNaN(num) ? null : num;
            } else if (controlType === 'Number') {
                const num = parseFloat(copy[name]);
                copy[name] = Number.isNaN(num) ? null : num;
            }
        });

        if (copy.Id != null && copy.Id !== '') {
            copy.Id = parseInt(copy.Id, 10);
        } else {
            delete copy.Id;
        }

        return copy;
    }

    function validateBeforeSave() {
        if (!validateMasterForm()) return false;

        MetaForgeDetailRows.clearRowFieldErrors($('#detailGridBody'));
        const fields = getDetailFields().filter(f => f.IsRequired ?? f.isRequired);
        for (let i = 0; i < detailRows.length; i++) {
            for (const field of fields) {
                const name = field.PropertyName ?? field.propertyName;
                const val = detailRows[i][name];
                if (isEmptyRequiredValue(val, name)) {
                    MetaForgeDetailRows.showRowFieldError(
                        $('#detailGridBody'),
                        i,
                        name,
                        `${field.Label ?? field.label ?? name} is required.`);
                    return false;
                }
            }
        }
        return true;
    }

    function updateSummary() {
        const count = detailRows.length;
        $('#detailItemCount').text(count);

        const qtyField = getDetailFields().find(f => (f.PropertyName ?? f.propertyName)?.toLowerCase() === 'quantity');
        const priceField = getDetailFields().find(f => {
            const n = (f.PropertyName ?? f.propertyName)?.toLowerCase() ?? '';
            return n === 'unitprice' || n === 'price' || n === 'amount';
        });

        if (qtyField && priceField) {
            const qtyName = qtyField.PropertyName ?? qtyField.propertyName;
            const priceName = priceField.PropertyName ?? priceField.propertyName;
            let total = 0;
            detailRows.forEach(row => {
                const q = parseFloat(row[qtyName]) || 0;
                const p = parseFloat(row[priceName]) || 0;
                total += q * p;
            });
            $('#detailLineTotal').text(total.toFixed(2));
            $('#detailSummaryRow').removeClass('d-none');
        } else {
            $('#detailSummaryRow').addClass('d-none');
        }
    }

    function saveAll() {
        [...editState.editing].forEach(index => syncRowFromDom(index));
        MetaForgeDetailRows.clearEditState(editState);
        if (!validateBeforeSave()) return;

        const payload = {
            Master: DynamicForm.getData(),
            Details: detailRows.map(normalizeDetailRow),
            DeletedDetailIds: deletedDetailIds
        };

        const $btn = $('#btnSaveAll').prop('disabled', true);
        $.ajax({
            url: `/api/metaforge/masterdetail/${moduleCode}`,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload)
        }).then(() => {
            const formName = screen.MasterForm?.FormName ?? screen.masterForm?.formName;
            MetaForgeUi.showAlert(MetaForgeUi.formatSavedMessage(formName), 'success', 3000);
            if (config.onSaved) {
                if (listModal) listModal.hide();
                config.onSaved();
            } else {
                window.location = `/Modules/${moduleCode}`;
            }
        }).fail(function (xhr) {
            if (!DynamicForm.handleAjaxValidationError($('#masterForm'), xhr)) {
                const msg = xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? xhr.responseText ?? xhr.statusText;
                alert('Save failed: ' + msg);
            }
        }).always(() => {
            $btn.prop('disabled', false);
        });
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escapeAttr(value) {
        return escapeHtml(value).replace(/'/g, '&#39;');
    }

    function formatDateValue(value) {
        if (!value) return '';
        const dt = new Date(value);
        return isNaN(dt.getTime()) ? value : dt.toISOString().slice(0, 10);
    }

    function formatDateTimeValue(value) {
        if (!value) return '';
        const dt = new Date(value);
        if (isNaN(dt.getTime())) return value;
        const pad = n => String(n).padStart(2, '0');
        return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
    }

    return { init, loadAndOpen };
})();
