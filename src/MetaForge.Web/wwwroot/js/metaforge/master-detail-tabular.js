/**
 * Tabular Master-Detail Engine — master form + multiple detail grids in tabs.
 */
const MasterDetailTabular = (function () {
    let screen, moduleCode, permissions = {}, config = { onSaved: null };
    let listModal = null;
    let sections = {};

    function sectionKey(section) {
        return section.ChildEntity ?? section.childEntity;
    }

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

    function getSections() {
        return screen.DetailSections ?? screen.detailSections ?? [];
    }

    function getSectionState(key) {
        if (!sections[key]) {
            sections[key] = { rows: [], deletedIds: [], editState: MetaForgeDetailRows.createEditState() };
        }
        if (!sections[key].editState) {
            sections[key].editState = MetaForgeDetailRows.createEditState();
        }
        return sections[key];
    }

    function getDetailFields(section) {
        const formFields = section.DetailForm?.Fields ?? section.detailForm?.fields;
        if (formFields?.length) return formFields;

        return (section.DetailGrid?.Columns ?? section.detailGrid?.columns ?? []).map(col => ({
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
        config = { onSaved: null, ...(options || {}) };
        screen = screenData;
        moduleCode = module;
        permissions = perms || {};
        sections = {};

        getSections().forEach(section => {
            const key = sectionKey(section);
            sections[key] = {
                rows: section.DetailData ? section.DetailData.map(r => ({ ...r })) : [],
                deletedIds: [],
                meta: section,
                editState: MetaForgeDetailRows.createEditState()
            };
        });

        $('#tabularMasterForm').attr('data-module', moduleCode).attr('data-entity', screen.MasterForm?.EntityName ?? '');
        DynamicForm.init('#tabularMasterForm', screen.MasterForm, { layoutClass: 'master-detail-form' });
        if (screen.MasterData) {
            DynamicForm.setDataWhenReady(screen.MasterData);
        } else {
            DynamicForm.refreshLookups();
        }

        if (!canModifyDetails()) {
            $('#tabularMasterForm :input').prop('disabled', true);
        }

        renderTabs();
        configureActionButtons();
        bindEvents();
    }

    function loadAndOpen(module, recordId, perms, options) {
        const url = recordId
            ? `/api/metaforge/masterdetail/${module}/${recordId}`
            : `/api/metaforge/masterdetail/${module}`;

        return $.getJSON(url).done(screenData => {
            if ((screenData.ScreenMode ?? screenData.screenMode) !== 'Tabular') {
                MetaForgeUi.showAlert('This form is not configured as tabular master-detail.', 'warning');
                return;
            }

            init(screenData, module, perms, options);

            const moduleName = screenData.MasterForm?.FormName ?? 'Record';
            const title = recordId ? `Edit ${moduleName}` : `New ${moduleName}`;
            $('#masterDetailTabularModalTitle').text(title);

            listModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('masterDetailTabularModal'));
            listModal.show();
        }).fail(function (xhr) {
            MetaForgeUi.showAlert('Failed to load entry: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger');
        });
    }

    function configureActionButtons() {
        $('#btnSaveTabularAll').toggleClass('d-none', !canSave());
    }

    function renderTabs() {
        const $tabs = $('#detailSectionTabs').empty();
        const $content = $('#detailSectionTabContent').empty();
        const sectionList = getSections();

        sectionList.forEach((section, index) => {
            const key = sectionKey(section);
            const tabId = `detail-tab-${key}`;
            const label = section.TabLabel ?? section.tabLabel ?? key;
            const active = index === 0 ? 'active' : '';
            const selected = index === 0 ? 'true' : 'false';

            $tabs.append(`
                <li class="nav-item" role="presentation">
                    <button class="nav-link ${active}" id="${tabId}-tab" data-bs-toggle="tab"
                        data-bs-target="#${tabId}" type="button" role="tab"
                        aria-controls="${tabId}" aria-selected="${selected}">
                        ${escapeHtml(label)}
                        <span class="badge bg-secondary ms-1 detail-tab-count" data-section="${escapeAttr(key)}">0</span>
                    </button>
                </li>`);

            $content.append(`
                <div class="tab-pane fade ${index === 0 ? 'show active' : ''}" id="${tabId}" role="tabpanel" aria-labelledby="${tabId}-tab">
                    <div class="d-flex justify-content-between align-items-center mb-2">
                        <span class="text-muted small">${escapeHtml(section.DetailForm?.FormName ?? key)}</span>
                        <div class="d-flex align-items-center gap-2">
                            <button type="button" class="btn btn-sm btn-teal btn-icon btn-add-section-row d-none" data-section="${escapeAttr(key)}" title="Add Row" aria-label="Add Row">
                                <i class="fa-solid fa-plus"></i>
                            </button>
                        </div>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-bordered table-hover master-detail-grid mb-0" data-section="${escapeAttr(key)}">
                            <thead><tr class="detail-grid-head" data-section="${escapeAttr(key)}"></tr></thead>
                            <tbody class="detail-grid-body" data-section="${escapeAttr(key)}"></tbody>
                        </table>
                    </div>
                </div>`);
        });

        sectionList.forEach(section => {
            renderSectionHeader(section);
            renderSectionGrid(section);
            MetaForgeDetailRows.resolveDisplayLabels(getSectionState(sectionKey(section)).rows, getDetailFields(section))
                .always(() => renderSectionGrid(section));
        });

        $('.btn-add-section-row').toggleClass('d-none', !canCreate());
    }

    function renderSectionHeader(section) {
        const key = sectionKey(section);
        const $head = $(`.detail-grid-head[data-section="${key}"]`);
        const fields = getDetailFields(section).filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');

        $head.empty().append('<th style="width:48px">#</th>');
        fields.forEach(f => {
            const label = f.Label ?? f.label ?? f.PropertyName ?? f.propertyName;
            $head.append(`<th>${MetaForgeDetailRows.escapeHtml(label)}</th>`);
        });
        if (canEdit() || canDelete()) {
            $head.append('<th style="width:120px" class="text-center">Actions</th>');
        }
    }

    function renderSectionGrid(section) {
        const key = sectionKey(section);
        const state = getSectionState(key);
        const $body = $(`.detail-grid-body[data-section="${key}"]`);
        const fields = getDetailFields(section);
        const visibleFields = fields.filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');
        const actionCols = (canEdit() || canDelete()) ? 1 : 0;
        const colspan = visibleFields.length + actionCols + 1;

        $body.empty();
        if (state.rows.length === 0) {
            $body.append(`<tr class="detail-empty-row"><td colspan="${colspan}" class="text-center text-muted py-4">No rows yet. Click add to begin.</td></tr>`);
        } else {
            state.rows.forEach((row, index) => {
                $body.append(buildDetailRow(section, row, index, fields));
            });
            initLookups($body, key);
        }

        updateSectionCount(key);
    }

    function buildDetailRow(section, row, index, fields) {
        const key = sectionKey(section);
        const state = getSectionState(key);
        const editing = MetaForgeDetailRows.isEditing(state.editState, index);
        const visibleFields = fields.filter(f => (f.ControlType ?? f.controlType) !== 'Hidden');
        const lineCell = `<td class="text-muted line-number">${index + 1}</td>`;
        const idValue = row.Id ?? row.id ?? '';
        const hiddenId = `<input type="hidden" class="detail-id" data-section="${MetaForgeDetailRows.escapeAttr(key)}" data-index="${index}" value="${MetaForgeDetailRows.escapeAttr(idValue)}" />`;
        const rowClass = editing ? 'detail-row-editing' : 'detail-row-view';
        const sectionAttr = `data-section="${MetaForgeDetailRows.escapeAttr(key)}"`;

        const cols = visibleFields.map(field => {
            const name = field.PropertyName ?? field.propertyName;
            const val = row[name];
            if (editing && canModifyDetails() && !(field.IsReadOnly ?? field.isReadOnly)) {
                return `<td>${MetaForgeDetailRows.buildInlineControl(field, index, val, {
                    readOnly: false,
                    dataAttrs: { 'data-section': key }
                })}</td>`;
            }
            return `<td>${MetaForgeDetailRows.buildDisplayCell(field, row)}</td>`;
        }).join('');

        const actionCell = MetaForgeDetailRows.buildActionCell(row, index, {
            canEdit: canEdit() && canModifyDetails(),
            canDelete: canDelete(),
            editing,
            actionDataAttrs: sectionAttr
        });

        return `<tr class="${rowClass}" ${sectionAttr} data-index="${index}">${hiddenId}${lineCell}${cols}${actionCell}</tr>`;
    }

    function initLookups($container, sectionKeyValue) {
        if (typeof MetaForgeLookups !== 'undefined') {
            const section = getSections().find(s => sectionKey(s) === sectionKeyValue);
            const fields = section ? getDetailFields(section) : [];
            MetaForgeLookups.initGridLookups($container, fields, index => getSectionState(sectionKeyValue).rows[index]);
            return;
        }

        $container.find('.lookup-select').each(function () {
            const $sel = $(this);
            const entity = $sel.data('lookup');
            const index = $sel.data('index');
            const field = $sel.data('field');
            const state = getSectionState(sectionKeyValue);
            const currentVal = state.rows[index]?.[field];

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
        $('#masterDetailTabularModal').off('click.tabular', '.btn-add-section-row').on('click.tabular', '.btn-add-section-row', function () {
            const key = $(this).data('section');
            const state = getSectionState(key);
            state.rows.push({});
            MetaForgeDetailRows.enterEdit(state.editState, state.rows, state.rows.length - 1);
            const section = getSections().find(s => sectionKey(s) === key);
            if (section) renderSectionGrid(section);
        });

        $('#masterDetailTabularModal').off('click.tabular', '.btn-detail-row-edit').on('click.tabular', '.btn-detail-row-edit', function () {
            const key = $(this).data('section');
            const index = $(this).data('index');
            const state = getSectionState(key);
            MetaForgeDetailRows.enterEdit(state.editState, state.rows, index);
            const section = getSections().find(s => sectionKey(s) === key);
            if (section) renderSectionGrid(section);
        });

        $('#masterDetailTabularModal').off('click.tabular', '.btn-detail-row-done').on('click.tabular', '.btn-detail-row-done', function () {
            const key = $(this).data('section');
            const index = $(this).data('index');
            syncRowFromDom(key, index);
            MetaForgeDetailRows.finishEdit(getSectionState(key).editState, index);
            const section = getSections().find(s => sectionKey(s) === key);
            if (section) renderSectionGrid(section);
        });

        $('#masterDetailTabularModal').off('click.tabular', '.btn-detail-row-cancel').on('click.tabular', '.btn-detail-row-cancel', function () {
            const key = $(this).data('section');
            const index = $(this).data('index');
            const section = getSections().find(s => sectionKey(s) === key);
            const state = getSectionState(key);
            MetaForgeDetailRows.cancelEdit(state.editState, state.rows, index, section ? getDetailFields(section) : []);
            if (section) renderSectionGrid(section);
        });

        $('#masterDetailTabularModal').off('click.tabular', '.btn-remove-detail').on('click.tabular', '.btn-remove-detail', function () {
            const key = $(this).data('section');
            const index = $(this).data('index');
            const state = getSectionState(key);
            const row = state.rows[index];
            const id = row?.Id ?? row?.id;
            const isPersisted = id != null && id !== '' && parseInt(id, 10) > 0;
            const section = getSections().find(s => sectionKey(s) === key);
            const label = section?.TabLabel ?? section?.tabLabel ?? 'row';

            MetaForgeUi.confirmDelete({
                title: 'Remove Line Item',
                message: isPersisted ? `Remove ${label.toLowerCase()} #${index + 1}?` : `Remove unsaved ${label.toLowerCase()} #${index + 1}?`,
                detail: isPersisted ? 'The row will be deleted when you save the document.' : 'This row has not been saved yet.'
            }).then(function (confirmed) {
                if (!confirmed) return;
                if (isPersisted) state.deletedIds.push(parseInt(id, 10));
                state.rows.splice(index, 1);
                MetaForgeDetailRows.clearEditState(state.editState);
                if (section) renderSectionGrid(section);
            });
        });

        $('#masterDetailTabularModal').off('change.tabular input.tabular', '.detail-input')
            .on('change.tabular input.tabular', '.detail-input', function () {
                syncRowFromDom($(this).data('section'), $(this).data('index'));
            });

        $('#btnSaveTabularAll').off('click').on('click', saveAll);
    }

    function syncRowFromDom(sectionKeyValue, index) {
        const state = getSectionState(sectionKeyValue);
        if (index == null || !state.rows[index]) return;

        const section = getSections().find(s => sectionKey(s) === sectionKeyValue);
        if (!section) return;

        getDetailFields(section).forEach(field => {
            const name = field.PropertyName ?? field.propertyName;
            const $input = $(`.detail-grid-body[data-section="${sectionKeyValue}"] [data-field="${name}"][data-index="${index}"]`);
            if ($input.length === 0) return;

            if ($input.attr('type') === 'checkbox') {
                state.rows[index][name] = $input.is(':checked');
            } else {
                state.rows[index][name] = $input.val();
            }

            MetaForgeDetailRows.syncDisplayFromInput(state.rows[index], field, $input);

            const controlType = field.ControlType ?? field.controlType ?? 'TextBox';
            if (controlType === 'Number') {
                const num = parseFloat(state.rows[index][name]);
                state.rows[index][name] = Number.isNaN(num) ? null : num;
            } else if ((controlType === 'Dropdown' || controlType === 'Autocomplete') && name.endsWith('Id')) {
                const num = parseInt(state.rows[index][name], 10);
                state.rows[index][name] = Number.isNaN(num) ? null : num;
            }
        });

        const idVal = $(`.detail-grid-body[data-section="${sectionKeyValue}"] .detail-id[data-index="${index}"]`).val();
        if (idVal) state.rows[index].Id = parseInt(idVal, 10);
    }

    function syncAllRowsFromDom() {
        getSections().forEach(section => {
            const key = sectionKey(section);
            const state = getSectionState(key);
            [...state.editState.editing].forEach(index => syncRowFromDom(key, index));
        });
    }

    function normalizeDetailRow(section, row) {
        const copy = { ...row };
        delete copy.__display;
        getDetailFields(section).forEach(field => {
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

        if (copy.Id != null && copy.Id !== '') copy.Id = parseInt(copy.Id, 10);
        else delete copy.Id;

        return copy;
    }

    function isEmptyRequiredValue(val, fieldName) {
        if (val == null || val === '') return true;
        if (fieldName && fieldName.endsWith('Id') && (val === 0 || val === '0')) return true;
        return false;
    }

    function validateMasterForm() {
        const fields = screen.MasterForm?.Fields ?? [];
        const errors = DynamicForm.validateRequiredFields($('#masterForm'), fields, DynamicForm.getData());
        return !errors;
    }

    function validateBeforeSave() {
        if (!validateMasterForm()) return false;

        for (const section of getSections()) {
            const key = sectionKey(section);
            const state = getSectionState(key);
            const fields = getDetailFields(section).filter(f => f.IsRequired ?? f.isRequired);
            const label = section.TabLabel ?? section.tabLabel ?? key;
            const $grid = $(`.detail-grid-body[data-section="${key}"]`);

            MetaForgeDetailRows.clearRowFieldErrors($grid);

            for (let i = 0; i < state.rows.length; i++) {
                for (const field of fields) {
                    const name = field.PropertyName ?? field.propertyName;
                    const val = state.rows[i][name];
                    if (isEmptyRequiredValue(val, name)) {
                        MetaForgeDetailRows.showRowFieldError(
                            $grid,
                            i,
                            name,
                            `${field.Label ?? field.label ?? name} is required.`);
                        return false;
                    }
                }
            }
        }
        return true;
    }

    function saveAll() {
        getSections().forEach(section => {
            const key = sectionKey(section);
            const state = getSectionState(key);
            [...state.editState.editing].forEach(index => syncRowFromDom(key, index));
            MetaForgeDetailRows.clearEditState(state.editState);
        });
        if (!validateBeforeSave()) return;

        const payload = {
            Master: DynamicForm.getData(),
            DetailSections: getSections().map(section => {
                const key = sectionKey(section);
                const state = getSectionState(key);
                return {
                    ChildEntity: key,
                    Rows: state.rows.map(row => normalizeDetailRow(section, row)),
                    DeletedIds: state.deletedIds
                };
            })
        };

        const $btn = $('#btnSaveTabularAll').prop('disabled', true);
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
            }
        }).fail(function (xhr) {
            if (!DynamicForm.handleAjaxValidationError($('#masterForm'), xhr)) {
                MetaForgeUi.showAlert(
                    xhr.responseJSON?.error ?? xhr.responseJSON?.title ?? xhr.responseText ?? xhr.statusText ?? 'Save failed.',
                    'danger');
            }
        }).always(() => {
            $btn.prop('disabled', false);
        });
    }

    function updateSectionCount(sectionKeyValue) {
        const count = getSectionState(sectionKeyValue).rows.length;
        $(`.detail-tab-count[data-section="${sectionKeyValue}"]`).text(count);
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
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
