/**
 * Form Builder — design master and detail forms for admin screens.
 */
const FormBuilder = (function () {
    const CONTROL_TYPES = ['TextBox', 'TextArea', 'Number', 'Date', 'DateTime', 'Checkbox', 'Dropdown', 'Autocomplete', 'Radio', 'FileUpload', 'Hidden'];
    const RELATION_TYPES = ['OneToOne', 'OneToMany', 'ManyToOne'];
    const GRID_ACTION_PLACEMENTS = ['Row', 'Toolbar'];
    const GRID_HANDLER_TYPES = ['Api', 'Redirect', 'Script'];
    const GRID_HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];
    const PERMISSION_ACTIONS = ['', 'View', 'Create', 'Edit', 'Delete', 'Export', 'Approve'];
    const BUTTON_STYLES = ['outline-primary', 'outline-success', 'outline-warning', 'outline-danger', 'outline-secondary', 'primary', 'success', 'danger'];

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }

        window.alert(message);
    }

    let state = {
        masterId: 0,
        detailId: 0,
        isEdit: false,
        screenType: 'Master',
        detailEntity: null,
        detailForeignKey: null,
        entities: []
    };

    function init() {
        const $app = $('#formBuilderApp');
        state.isEdit = $app.data('is-edit') === true || $app.data('is-edit') === 'true';
        state.entities = window.__formBuilderData?.entities || [];

        $('#groupName').val($app.data('default-group') || 'Master Data');
        $('#screenType').val($app.data('default-screen-type') || 'Master');

        if (typeof ValidationRuleBuilder !== 'undefined') {
            ValidationRuleBuilder.init();
        }

        if (typeof ConditionalRuleBuilder !== 'undefined') {
            ConditionalRuleBuilder.init();
        }

        if (typeof EntitySchemaSync !== 'undefined') {
            EntitySchemaSync.init({ onApplied: handleSchemaSyncApplied });
        }

        const screen = window.__formBuilderData?.screen;
        const legacy = window.__formBuilderData?.module;

        if (screen) {
            loadScreen(screen);
        } else if (legacy) {
            loadMasterConfig(legacy);
        }

        bindEvents();
        updateScreenTypeUi();
        refreshPreviews();
    }

    function bindEvents() {
        $('#btnLoadDraft').on('click', loadDraftFromEntity);
        $('#entitySelect').on('change', onEntitySelected);
        $('#screenType').on('change', onScreenTypeChanged);
        bindScreenTypeOptions();
        $('#btnAddMasterField').on('click', () => addFieldRow('#masterFieldsTable', {}, refreshMasterPreview));
        $('#btnAddDetailField').on('click', () => addFieldRow('#detailFieldsTable', {}, refreshDetailPreview));
        $('#btnAddColumn').on('click', () => addColumnRow());
        $(document).on('change blur', '#columnsTable .col-prop', function () {
            syncColumnFormatSelect($(this).closest('tr'));
        });
        $(document).on('change', '#masterFieldsTable .field-control, #masterFieldsTable .field-prop, #detailFieldsTable .field-control, #detailFieldsTable .field-prop', function () {
            $('#columnsTable tbody tr').each(function () {
                syncColumnFormatSelect($(this));
            });
        });
        $('#btnAddGridAction').on('click', () => addGridActionRow());
        $('#btnAddRelation').on('click', () => addRelationRow());
        $('#btnSaveScreen').on('click', saveScreen);
        $('#btnSyncFromEntity').on('click', openSchemaSync);

        $(document).on('click', '.btn-remove-row', function () {
            const $table = $(this).closest('table');
            $(this).closest('tr').remove();
            if ($table.is('#masterFieldsTable')) refreshMasterPreview();
            if ($table.is('#detailFieldsTable')) refreshDetailPreview();
        });

        $(document).on('click', '.btn-move-up', function () {
            const $row = $(this).closest('tr');
            const $prev = $row.prev();
            if ($prev.length) {
                $row.insertBefore($prev);
                onFieldTableChanged($row.closest('table'));
            }
        });

        $(document).on('click', '.btn-move-down', function () {
            const $row = $(this).closest('tr');
            const $next = $row.next();
            if ($next.length) {
                $row.insertAfter($next);
                onFieldTableChanged($row.closest('table'));
            }
        });

        $(document).on('change', '#masterFieldsTable input, #masterFieldsTable select', refreshMasterPreview);
        $(document).on('change', '#detailFieldsTable input, #detailFieldsTable select', refreshDetailPreview);
        $(document).on('change', '#relationsTable select, #relationsTable input', syncDetailFromRelations);
    }

    function onFieldTableChanged($table) {
        if ($table.is('#masterFieldsTable')) refreshMasterPreview();
        if ($table.is('#detailFieldsTable')) refreshDetailPreview();
    }

    function onEntitySelected() {
        const $opt = $('#entitySelect option:selected');
        $('#entityName').val($opt.val());
        $('#tableName').val($opt.data('table') || '');
        if (!$('#formCode').val()) {
            $('#formCode').val(($opt.val() || '').toLowerCase());
        }
        if (!$('#moduleName').val()) {
            $('#moduleName').val(splitPascalCase($opt.val() || ''));
        }
    }

    function onScreenTypeChanged() {
        state.screenType = $('#screenType').val();
        updateScreenTypeUi();
        if (state.screenType === 'MasterDetail' || state.screenType === 'MasterDetailTabular') {
            syncDetailFromRelations();
        }
        refreshMasterPreview();
    }

    function bindScreenTypeOptions() {
        $('.form-builder-type-option').on('click', function () {
            const type = $(this).data('type');
            $('#screenType').val(type);
            syncScreenTypeCards();
            onScreenTypeChanged();
        });
    }

    function syncScreenTypeCards() {
        const type = $('#screenType').val();
        $('.form-builder-type-option').removeClass('is-selected');
        $(`.form-builder-type-option[data-type="${type}"]`).addClass('is-selected');
        $(`.form-builder-type-option[data-type="${type}"] input[type="radio"]`).prop('checked', true);
    }

    function updateScreenTypeUi() {
        const screenType = $('#screenType').val();
        const isMasterDetail = screenType === 'MasterDetail' || screenType === 'MasterDetailTabular';
        const isTabbed = screenType === 'Tabbed';
        $('#tab-detail-nav').toggleClass('d-none', !isMasterDetail);
        $('#detailEntityInfo').toggleClass('d-none', !isMasterDetail || !state.detailEntity);
        $('#tabbedSectionHint').toggleClass('d-none', !isTabbed);
        syncScreenTypeCards();

        if (isMasterDetail) {
            $('#groupName').val('Transaction');
        }
    }

    function loadScreen(screen) {
        state.screenType = screen.ScreenType ?? screen.screenType ?? 'Master';
        $('#screenType').val(state.screenType);

        loadMasterConfig(screen.Master ?? screen.master);
        if (screen.Detail ?? screen.detail) {
            loadDetailConfig(screen.Detail ?? screen.detail);
        }

        updateScreenTypeUi();
    }

    function loadMasterConfig(config) {
        state.masterId = config.Id ?? config.id ?? 0;
        $('#formCode').val(config.Code ?? config.code ?? '');
        $('#moduleName').val(config.Name ?? config.name ?? '');
        $('#entityName').val(config.EntityName ?? config.entityName ?? '');
        $('#tableName').val(config.TableName ?? config.tableName ?? '');
        $('#groupName').val(config.GroupName ?? config.groupName ?? 'Master Data');
        $('#displayOrder').val(config.DisplayOrder ?? config.displayOrder ?? 0);
        $('#isActive').prop('checked', config.IsActive ?? config.isActive ?? true);
        $('#entitySelect').val(config.EntityName ?? config.entityName ?? '');

        renderFields('#masterFieldsTable', config.Fields ?? config.fields ?? [], refreshMasterPreview);
        renderColumns(config.GridColumns ?? config.gridColumns ?? []);
        renderGridActions(config.GridActions ?? config.gridActions ?? []);
        renderRelations(config.Relations ?? config.relations ?? []);
        syncDetailFromRelations();
    }

    function loadDetailConfig(config) {
        state.detailId = config.Id ?? config.id ?? 0;
        renderFields('#detailFieldsTable', config.Fields ?? config.fields ?? [], refreshDetailPreview);
    }

    function loadDraftFromEntity() {
        const entity = $('#entitySelect').val();
        const group = $('#groupName').val();
        if (!entity) {
            notify('Please select an entity first.', 'warning');
            return;
        }

        $.getJSON(`/api/metaforge/formconfig/draft/${entity}?groupName=${encodeURIComponent(group)}`)
            .done(async function (draft) {
                loadMasterConfig(draft);

                if ($('#screenType').val() === 'MasterDetail' || $('#screenType').val() === 'MasterDetailTabular') {
                    await loadDetailDraftFromRelations();
                }

                refreshPreviews();
            })
            .fail(xhr => notify('Failed to load draft: ' + (xhr.responseJSON?.error ?? xhr.statusText), 'danger'));
    }

    async function loadDetailDraftFromRelations() {
        const rel = getPrimaryOneToManyRelation();
        if (!rel) return;

        state.detailEntity = rel.ChildEntity ?? rel.childEntity;
        state.detailForeignKey = rel.ForeignKey ?? rel.foreignKey;
        $('#detailEntityName').text(state.detailEntity);
        $('#detailForeignKey').text(state.detailForeignKey);
        $('#detailEntityInfo').removeClass('d-none');

        try {
            let detail = null;
            try {
                detail = await $.getJSON(`/api/metaforge/formconfig/by-entity/${state.detailEntity}`);
            } catch {
                detail = await $.getJSON(`/api/metaforge/formconfig/draft/${state.detailEntity}?groupName=${encodeURIComponent($('#groupName').val())}`);
            }
            loadDetailConfig(detail);
        } catch {
            /* detail entity may not exist yet */
        }
    }

    function syncDetailFromRelations() {
        const screenType = $('#screenType').val();
        if (screenType !== 'MasterDetail' && screenType !== 'MasterDetailTabular') return;

        const rel = getPrimaryOneToManyRelation();
        if (!rel) {
            state.detailEntity = null;
            $('#detailEntityInfo').addClass('d-none');
            return;
        }

        state.detailEntity = rel.ChildEntity ?? rel.childEntity;
        state.detailForeignKey = rel.ForeignKey ?? rel.foreignKey;
        $('#detailEntityName').text(state.detailEntity);
        $('#detailForeignKey').text(state.detailForeignKey);
        $('#detailEntityInfo').removeClass('d-none');

        if ($('#detailFieldsTable tbody tr').length === 0) {
            loadDetailDraftFromRelations();
        }
    }

    function getPrimaryOneToManyRelation() {
        let found = null;
        $('#relationsTable tbody tr').each(function (index) {
            const type = $(this).find('.rel-type').val();
            if (type === 'OneToMany' && !found) {
                found = collectRelationRow($(this), index);
            }
        });
        return found;
    }

    function getOneToManyRelations() {
        const relations = [];
        $('#relationsTable tbody tr').each(function (index) {
            const type = $(this).find('.rel-type').val();
            if (type === 'OneToMany') {
                relations.push(collectRelationRow($(this), index));
            }
        });
        return relations;
    }

    function collectRelationRow($row, index) {
        return {
            RelationType: $row.find('.rel-type').val(),
            ParentEntity: $row.find('.rel-parent').val()?.trim(),
            ChildEntity: $row.find('.rel-child').val()?.trim(),
            ForeignKey: $row.find('.rel-fk').val()?.trim(),
            NavigationProperty: $row.find('.rel-nav').val()?.trim() || null,
            TabLabel: $row.find('.rel-tab-label').val()?.trim() || null,
            DisplayOrder: parseInt($row.find('.rel-order').val(), 10) || index
        };
    }

    function renderFields(tableSelector, fields, previewFn) {
        const $tbody = $(`${tableSelector} tbody`).empty();
        fields.forEach(f => addFieldRow(tableSelector, f, null, false));
        if (fields.length === 0) addFieldRow(tableSelector, {}, null, false);
        if (previewFn) previewFn();
    }

    function renderColumns(columns) {
        const $tbody = $('#columnsTable tbody').empty();
        columns.forEach(c => addColumnRow(c));
        if (columns.length === 0) addColumnRow();
    }

    function renderRelations(relations) {
        const $tbody = $('#relationsTable tbody').empty();
        relations.forEach(r => addRelationRow(r));
    }

    function addFieldRow(tableSelector, field = {}, previewFn = refreshPreviews, triggerPreview = true) {
        const controlOptions = CONTROL_TYPES.map(c =>
            `<option value="${c}" ${(field.ControlType ?? field.controlType) === c ? 'selected' : ''}>${c}</option>`).join('');
        const validationRule = field.ValidationRule ?? field.validationRule ?? '';
        const conditionalRule = field.ConditionalRule ?? field.conditionalRule ?? '';
        const validationCell = typeof ValidationRuleBuilder !== 'undefined'
            ? ValidationRuleBuilder.renderValidationCell()
            : `<td><input type="text" class="form-control form-control-sm field-validation" placeholder="MaxLength:50" /></td>`;
        const conditionalCell = typeof ConditionalRuleBuilder !== 'undefined'
            ? ConditionalRuleBuilder.renderConditionalCell()
            : `<td><input type="hidden" class="field-conditional" /></td>`;

        const $row = $(`
            <tr>
                <td class="col-order">
                    <div class="btn-group-vertical btn-group-sm">
                        <button type="button" class="btn btn-outline-secondary btn-sm btn-move-up" title="Move Up" aria-label="Move Up"><i class="fa-solid fa-chevron-up"></i></button>
                        <button type="button" class="btn btn-outline-secondary btn-sm btn-move-down" title="Move Down" aria-label="Move Down"><i class="fa-solid fa-chevron-down"></i></button>
                    </div>
                </td>
                <td><input type="text" class="form-control form-control-sm field-prop" value="${esc(field.PropertyName ?? field.propertyName ?? '')}" /></td>
                <td><input type="text" class="form-control form-control-sm field-label" value="${esc(field.Label ?? field.label ?? '')}" /></td>
                <td><select class="form-select form-select-sm field-control">${controlOptions}</select></td>
                <td><input type="text" class="form-control form-control-sm field-section" value="${esc(field.SectionName ?? field.sectionName ?? '')}" placeholder="optional" /></td>
                <td><input type="text" class="form-control form-control-sm field-lookup" value="${esc(field.LookupEntity ?? field.lookupEntity ?? '')}" placeholder="e.g. Country" /></td>
                <td><input type="text" class="form-control form-control-sm field-cascade-parent" value="${esc(field.LookupParentField ?? field.lookupParentField ?? '')}" placeholder="e.g. CountryId" /></td>
                <td><input type="text" class="form-control form-control-sm field-cascade-filter" value="${esc(field.LookupFilterField ?? field.lookupFilterField ?? '')}" placeholder="optional" /></td>
                <td class="text-center"><input type="checkbox" class="form-check-input field-required" ${(field.IsRequired ?? field.isRequired) ? 'checked' : ''} /></td>
                <td class="text-center"><input type="checkbox" class="form-check-input field-visible" ${(field.IsVisible ?? field.isVisible ?? true) ? 'checked' : ''} /></td>
                <td class="text-center"><input type="checkbox" class="form-check-input field-readonly" ${(field.IsReadOnly ?? field.isReadOnly) ? 'checked' : ''} /></td>
                ${validationCell}
                ${conditionalCell}
                <td><button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-row" title="Remove" aria-label="Remove"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`);

        $(`${tableSelector} tbody`).append($row);

        if (typeof ValidationRuleBuilder !== 'undefined') {
            ValidationRuleBuilder.setRowValidationRule($row, validationRule);
        } else {
            $row.find('.field-validation').val(validationRule);
        }

        if (typeof ConditionalRuleBuilder !== 'undefined') {
            ConditionalRuleBuilder.setRowConditionalRule($row, conditionalRule);
        } else {
            $row.find('.field-conditional').val(conditionalRule);
        }

        if (triggerPreview && previewFn) previewFn();
    }

    function renderGridActions(actions) {
        const $tbody = $('#gridActionsTable tbody').empty();
        actions.forEach(a => addGridActionRow(a));
    }

    function addGridActionRow(action = {}) {
        const placementOptions = GRID_ACTION_PLACEMENTS.map(p =>
            `<option value="${p}" ${(action.Placement ?? action.placement ?? 'Row') === p ? 'selected' : ''}>${p}</option>`).join('');
        const handlerOptions = GRID_HANDLER_TYPES.map(h =>
            `<option value="${h}" ${(action.HandlerType ?? action.handlerType ?? 'Api') === h ? 'selected' : ''}>${h}</option>`).join('');
        const methodOptions = GRID_HTTP_METHODS.map(m =>
            `<option value="${m}" ${(action.HttpMethod ?? action.httpMethod ?? 'POST') === m ? 'selected' : ''}>${m}</option>`).join('');
        const permissionOptions = PERMISSION_ACTIONS.map(p =>
            `<option value="${p}" ${(action.PermissionAction ?? action.permissionAction ?? '') === p ? 'selected' : ''}>${p || '(View)'}</option>`).join('');
        const styleOptions = BUTTON_STYLES.map(s =>
            `<option value="${s}" ${(action.ButtonStyle ?? action.buttonStyle ?? 'outline-primary') === s ? 'selected' : ''}>${s}</option>`).join('');

        $('#gridActionsTable tbody').append(`
            <tr>
                <td><input type="text" class="form-control form-control-sm action-code" value="${esc(action.Code ?? action.code ?? '')}" placeholder="approve" /></td>
                <td><input type="text" class="form-control form-control-sm action-label" value="${esc(action.Label ?? action.label ?? '')}" placeholder="Approve" /></td>
                <td><input type="text" class="form-control form-control-sm action-icon" value="${esc(action.Icon ?? action.icon ?? '')}" placeholder="check" /></td>
                <td><select class="form-select form-select-sm action-placement">${placementOptions}</select></td>
                <td><select class="form-select form-select-sm action-handler">${handlerOptions}</select></td>
                <td><input type="text" class="form-control form-control-sm action-target" value="${esc(action.HandlerTarget ?? action.handlerTarget ?? '')}" placeholder="/api/.../{id}" /></td>
                <td><select class="form-select form-select-sm action-method">${methodOptions}</select></td>
                <td><input type="text" class="form-control form-control-sm action-body" value="${esc(action.RequestBody ?? action.requestBody ?? '')}" placeholder='{"Status":"Approved"}' /></td>
                <td><select class="form-select form-select-sm action-permission">${permissionOptions}</select></td>
                <td><input type="text" class="form-control form-control-sm action-confirm" value="${esc(action.ConfirmMessage ?? action.confirmMessage ?? '')}" placeholder="optional" /></td>
                <td><select class="form-select form-select-sm action-style">${styleOptions}</select></td>
                <td class="text-center"><input type="checkbox" class="form-check-input action-active" ${(action.IsActive ?? action.isActive ?? true) ? 'checked' : ''} /></td>
                <td><button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-row" title="Remove" aria-label="Remove"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`);
    }

    function getFieldControlType(propertyName) {
        if (!propertyName) return '';
        const prop = propertyName.trim().toLowerCase();
        let controlType = '';
        $('#masterFieldsTable tbody tr, #detailFieldsTable tbody tr').each(function () {
            const rowProp = $(this).find('.field-prop').val()?.trim().toLowerCase();
            if (rowProp === prop) {
                controlType = $(this).find('.field-control').val() || '';
                return false;
            }
        });
        return controlType;
    }

    function buildColumnFormatSelect(selected, controlType) {
        const options = typeof MetaForgeGridDisplayFormat !== 'undefined'
            ? MetaForgeGridDisplayFormat.buildSelectOptions(selected)
            : `<option value="">Default</option>`;
        const temporal = typeof MetaForgeGridDisplayFormat !== 'undefined'
            ? MetaForgeGridDisplayFormat.isTemporalControlType(controlType)
            : false;
        const disabled = temporal ? '' : ' disabled';
        const title = temporal
            ? 'Date or date-time display format for this column'
            : 'Only applies to Date or DateTime fields';
        return `<select class="form-select form-select-sm col-display-format"${disabled} title="${title}">${options}</select>`;
    }

    function syncColumnFormatSelect($row) {
        const prop = $row.find('.col-prop').val()?.trim();
        const controlType = getFieldControlType(prop);
        const $select = $row.find('.col-display-format');
        const selected = $select.val() || '';
        const temporal = typeof MetaForgeGridDisplayFormat !== 'undefined'
            && MetaForgeGridDisplayFormat.isTemporalControlType(controlType);

        $select.prop('disabled', !temporal);
        if (temporal && !selected && typeof MetaForgeGridDisplayFormat !== 'undefined') {
            $select.val(MetaForgeGridDisplayFormat.getDefaultForControlType(controlType));
        }
    }

    function addColumnRow(col = {}) {
        const prop = col.PropertyName ?? col.propertyName ?? '';
        const controlType = getFieldControlType(prop);
        const displayFormat = col.DisplayFormat ?? col.displayFormat
            ?? (typeof MetaForgeGridDisplayFormat !== 'undefined'
                ? MetaForgeGridDisplayFormat.getDefaultForControlType(controlType)
                : '');
        const formatSelect = buildColumnFormatSelect(displayFormat, controlType);

        const $row = $(`
            <tr>
                <td><input type="text" class="form-control form-control-sm col-prop" value="${esc(prop)}" /></td>
                <td><input type="text" class="form-control form-control-sm col-label" value="${esc(col.Label ?? col.label ?? '')}" /></td>
                <td>${formatSelect}</td>
                <td class="text-center"><input type="checkbox" class="form-check-input col-sortable" ${(col.IsSortable ?? col.isSortable ?? true) ? 'checked' : ''} /></td>
                <td class="text-center"><input type="checkbox" class="form-check-input col-searchable" ${(col.IsSearchable ?? col.isSearchable) ? 'checked' : ''} /></td>
                <td class="text-center"><input type="checkbox" class="form-check-input col-visible" ${(col.IsVisible ?? col.isVisible ?? true) ? 'checked' : ''} /></td>
                <td><button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-row" title="Remove" aria-label="Remove"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`);

        $('#columnsTable tbody').append($row);
        syncColumnFormatSelect($row);
    }

    function addRelationRow(rel = {}) {
        const typeOptions = RELATION_TYPES.map(t =>
            `<option value="${t}" ${(rel.RelationType ?? rel.relationType) === t ? 'selected' : ''}>${t}</option>`).join('');

        $('#relationsTable tbody').append(`
            <tr>
                <td><select class="form-select form-select-sm rel-type">${typeOptions}</select></td>
                <td><input type="text" class="form-control form-control-sm rel-parent" value="${esc(rel.ParentEntity ?? rel.parentEntity ?? '')}" /></td>
                <td><input type="text" class="form-control form-control-sm rel-child" value="${esc(rel.ChildEntity ?? rel.childEntity ?? '')}" /></td>
                <td><input type="text" class="form-control form-control-sm rel-fk" value="${esc(rel.ForeignKey ?? rel.foreignKey ?? '')}" /></td>
                <td><input type="text" class="form-control form-control-sm rel-nav" value="${esc(rel.NavigationProperty ?? rel.navigationProperty ?? '')}" /></td>
                <td><input type="text" class="form-control form-control-sm rel-tab-label" value="${esc(rel.TabLabel ?? rel.tabLabel ?? '')}" placeholder="Tab name" /></td>
                <td><input type="number" class="form-control form-control-sm rel-order" value="${rel.DisplayOrder ?? rel.displayOrder ?? 0}" min="0" /></td>
                <td><button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-row" title="Remove" aria-label="Remove"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`);
    }

    function collectFields(tableSelector) {
        const fields = [];
        $(`${tableSelector} tbody tr`).each(function (i) {
            const prop = $(this).find('.field-prop').val()?.trim();
            if (!prop) return;
            fields.push({
                PropertyName: prop,
                Label: $(this).find('.field-label').val()?.trim() || prop,
                ControlType: $(this).find('.field-control').val(),
                SectionName: $(this).find('.field-section').val()?.trim() || null,
                LookupEntity: $(this).find('.field-lookup').val()?.trim() || null,
                LookupParentField: $(this).find('.field-cascade-parent').val()?.trim() || null,
                LookupFilterField: $(this).find('.field-cascade-filter').val()?.trim() || null,
                IsRequired: $(this).find('.field-required').is(':checked'),
                IsVisible: $(this).find('.field-visible').is(':checked'),
                IsReadOnly: $(this).find('.field-readonly').is(':checked'),
                ValidationRule: typeof ValidationRuleBuilder !== 'undefined'
                    ? ValidationRuleBuilder.getRowValidationRule($(this))
                    : ($(this).find('.field-validation').val()?.trim() || null),
                ConditionalRule: typeof ConditionalRuleBuilder !== 'undefined'
                    ? ConditionalRuleBuilder.getRowConditionalRule($(this))
                    : ($(this).find('.field-conditional').val()?.trim() || null),
                DisplayOrder: i
            });
        });
        return fields;
    }

    function collectMasterConfig() {
        const gridColumns = [];
        $('#columnsTable tbody tr').each(function (i) {
            const prop = $(this).find('.col-prop').val()?.trim();
            if (!prop) return;
            const displayFormat = $(this).find('.col-display-format').val()?.trim() || null;
            gridColumns.push({
                PropertyName: prop,
                Label: $(this).find('.col-label').val()?.trim() || prop,
                DisplayFormat: displayFormat,
                IsSortable: $(this).find('.col-sortable').is(':checked'),
                IsSearchable: $(this).find('.col-searchable').is(':checked'),
                IsVisible: $(this).find('.col-visible').is(':checked'),
                DisplayOrder: i
            });
        });

        const gridActions = [];
        $('#gridActionsTable tbody tr').each(function (i) {
            const code = $(this).find('.action-code').val()?.trim();
            const label = $(this).find('.action-label').val()?.trim();
            if (!code || !label) return;
            gridActions.push({
                Code: code,
                Label: label,
                Icon: $(this).find('.action-icon').val()?.trim() || null,
                Placement: $(this).find('.action-placement').val(),
                HandlerType: $(this).find('.action-handler').val(),
                HandlerTarget: $(this).find('.action-target').val()?.trim() || '',
                HttpMethod: $(this).find('.action-method').val(),
                RequestBody: $(this).find('.action-body').val()?.trim() || null,
                PermissionAction: $(this).find('.action-permission').val() || null,
                ConfirmMessage: $(this).find('.action-confirm').val()?.trim() || null,
                ButtonStyle: $(this).find('.action-style').val(),
                IsActive: $(this).find('.action-active').is(':checked'),
                DisplayOrder: i
            });
        });

        const relations = [];
        $('#relationsTable tbody tr').each(function (index) {
            const parent = $(this).find('.rel-parent').val()?.trim();
            const child = $(this).find('.rel-child').val()?.trim();
            if (!parent || !child) return;
            relations.push({
                RelationType: $(this).find('.rel-type').val(),
                ParentEntity: parent,
                ChildEntity: child,
                ForeignKey: $(this).find('.rel-fk').val()?.trim() || '',
                NavigationProperty: $(this).find('.rel-nav').val()?.trim() || null,
                TabLabel: $(this).find('.rel-tab-label').val()?.trim() || null,
                DisplayOrder: parseInt($(this).find('.rel-order').val(), 10) || index
            });
        });

        return {
            Id: state.masterId,
            Code: $('#formCode').val()?.trim(),
            Name: $('#moduleName').val()?.trim(),
            EntityName: $('#entityName').val()?.trim(),
            TableName: $('#tableName').val()?.trim(),
            GroupName: $('#groupName').val(),
            DisplayOrder: parseInt($('#displayOrder').val(), 10) || 0,
            IsActive: $('#isActive').is(':checked'),
            Fields: collectFields('#masterFieldsTable'),
            GridColumns: gridColumns,
            GridActions: gridActions,
            Relations: relations
        };
    }

    function buildGridColumnsFromFields(fields) {
        return (fields || [])
            .filter(f => (f.IsVisible ?? f.isVisible ?? true)
                && (f.ControlType ?? f.controlType) !== 'Hidden')
            .map((f, i) => {
                const controlType = f.ControlType ?? f.controlType;
                return {
                    PropertyName: f.PropertyName ?? f.propertyName,
                    Label: f.Label ?? f.label ?? f.PropertyName ?? f.propertyName,
                    DisplayOrder: i,
                    IsSortable: false,
                    IsSearchable: false,
                    IsVisible: true,
                    DisplayFormat: typeof MetaForgeGridDisplayFormat !== 'undefined'
                        ? (MetaForgeGridDisplayFormat.getDefaultForControlType(controlType) || null)
                        : null
                };
            });
    }

    function collectDetailConfig() {
        const rel = getPrimaryOneToManyRelation();
        if (!rel) return null;

        const entityMeta = state.entities.find(e =>
            (e.EntityName ?? e.entityName)?.toLowerCase() === (rel.ChildEntity ?? '').toLowerCase());

        const fields = collectFields('#detailFieldsTable');

        return {
            Id: state.detailId,
            Code: (rel.ChildEntity ?? '').toLowerCase(),
            Name: splitPascalCase(rel.ChildEntity ?? ''),
            EntityName: rel.ChildEntity,
            TableName: entityMeta?.TableName ?? entityMeta?.tableName ?? '',
            GroupName: $('#groupName').val(),
            DisplayOrder: (parseInt($('#displayOrder').val(), 10) || 0) + 1,
            IsActive: true,
            Fields: fields,
            GridColumns: buildGridColumnsFromFields(fields),
            Relations: []
        };
    }

    function saveScreen() {
        const screenType = $('#screenType').val();
        const master = collectMasterConfig();

        if (!master.Code || !master.Name || !master.EntityName) {
            notify('Code, display name, and entity are required.', 'warning');
            return;
        }
        if (master.Fields.length === 0) {
            notify('Master form requires at least one field.', 'warning');
            return;
        }
        if (master.GridColumns.length === 0) {
            notify('List grid requires at least one column.', 'warning');
            return;
        }

        const payload = {
            ScreenType: screenType,
            Master: master,
            Detail: null
        };

        if (screenType === 'MasterDetail') {
            const rel = getPrimaryOneToManyRelation();
            if (!rel) {
                notify('Master + Detail requires a OneToMany relation on the Relations tab.', 'warning');
                return;
            }
            payload.Detail = collectDetailConfig();
            if (!payload.Detail || payload.Detail.Fields.length === 0) {
                notify('Detail form requires at least one field.', 'warning');
                return;
            }
        }

        if (screenType === 'MasterDetailTabular') {
            const relations = getOneToManyRelations();
            if (relations.length === 0) {
                notify('Master + Tabular Details requires at least one OneToMany relation on the Relations tab.', 'warning');
                return;
            }
            payload.Detail = collectDetailConfig();
        }

        const $btn = $('#btnSaveScreen').prop('disabled', true);

        $.ajax({
            url: '/api/metaforge/formconfig/screen',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload)
        }).done(function () {
            notify('Form saved successfully.', 'success');
            window.setTimeout(function () {
                window.location = '/FormBuilder';
            }, 700);
        }).fail(function (xhr) {
            const msg = xhr.responseJSON?.error ?? xhr.responseText ?? xhr.statusText;
            notify('Save failed: ' + msg, 'danger');
        }).always(function () {
            $btn.prop('disabled', false);
        });
    }

    function refreshPreviews() {
        refreshMasterPreview();
        refreshDetailPreview();
    }

    function refreshMasterPreview() {
        const fields = collectFields('#masterFieldsTable');
        const screenType = $('#screenType').val();
        DynamicForm.renderPreview('#masterFormPreview', {
            FormName: $('#moduleName').val() || 'Master Form',
            FormType: screenType === 'Tabbed' ? 'Tabbed' : 'Master',
            Fields: fields
        }, {
            layoutClass: 'admin-form-preview-layout',
            layout: screenType === 'Tabbed' ? 'tabs' : 'sections',
            previewMode: true
        });
    }

    function refreshDetailPreview() {
        const fields = collectFields('#detailFieldsTable');
        DynamicForm.renderPreview('#detailFormPreview', {
            FormName: state.detailEntity || 'Detail Form',
            Fields: fields
        }, { layoutClass: 'admin-form-preview-layout admin-form-inline' });
    }

    function esc(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function splitPascalCase(value) {
        return value.replace(/([A-Z])/g, ' $1').trim();
    }

    function getActiveSyncTarget() {
        const activeTab = $('.form-builder-tabs .nav-link.active').attr('id');
        if (activeTab === 'tab-detail-btn') {
            if (!state.detailId || state.detailId <= 0) {
                return { formId: 0, label: 'Detail form' };
            }
            return {
                formId: state.detailId,
                label: `${state.detailEntity || 'Detail'} form`
            };
        }

        return {
            formId: state.masterId,
            label: `${$('#moduleName').val()?.trim() || 'Master'} (${$('#entityName').val()?.trim() || 'entity'})`
        };
    }

    function openSchemaSync() {
        const target = getActiveSyncTarget();
        if (!target.formId || target.formId <= 0) {
            notify('Save the form first, or switch to the master tab to sync the header form.', 'warning');
            return;
        }

        if (typeof EntitySchemaSync !== 'undefined') {
            EntitySchemaSync.open(target.formId, target.label);
        }
    }

    function handleSchemaSyncApplied(result) {
        const form = result?.Form ?? result?.form;
        if (!form) return;

        const formId = form.Id ?? form.id ?? 0;
        const entityName = form.EntityName ?? form.entityName ?? '';

        if (formId === state.detailId || (state.detailEntity && entityName === state.detailEntity)) {
            state.detailId = formId;
            loadDetailConfig(form);
        } else {
            state.masterId = formId;
            loadMasterConfig(form);
        }

        refreshPreviews();
    }

    return { init };
})();

/** @deprecated Use FormBuilder — kept for backward compatibility */
const ModuleConfig = FormBuilder;

$(function () { FormBuilder.init(); });
