/**
 * Report Builder — configure dynamic reports (columns, filters, grouping, totals).
 * Supports PascalCase DTOs from server-side JSON (PropertyNamingPolicy = null).
 */
const ReportBuilder = (function () {
    const COLUMN_ROLES = ['Detail', 'GroupBy', 'Aggregate', 'Calculated'];
    const FILTER_CONTROLS = ['TextBox', 'Dropdown', 'Autocomplete', 'DateRange'];
    const AGGREGATE_FUNCTIONS = ['None', 'Sum', 'Count', 'Avg', 'Min', 'Max'];
    const FILTER_OPERATORS = [
        'Equals', 'NotEquals', 'Contains', 'StartsWith',
        'GreaterThan', 'LessThan', 'GreaterOrEqual', 'LessOrEqual', 'Between'
    ];

    let state = {
        reportId: 0,
        isEdit: false,
        reportType: 'Tabular',
        entities: [],
        propertyPaths: []
    };

    function pick(obj, pascal, camel, fallback) {
        if (obj == null) return fallback;
        if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
        if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
        return fallback;
    }

    function notify(message, type) {
        if (typeof MetaForgeUi !== 'undefined') {
            MetaForgeUi.showAlert(message, type || 'danger');
            return;
        }
        window.alert(message);
    }

    function init() {
        const $app = $('#reportBuilderApp');
        state.isEdit = $app.data('is-edit') === true || $app.data('is-edit') === 'true';
        state.entities = window.__reportBuilderData?.entities || [];

        bindEvents();

        const report = window.__reportBuilderData?.report;
        if (report) {
            loadReport(report);
        }

        updateReportTypeUi();
    }

    function bindEvents() {
        $('#btnLoadDraft').on('click', loadDraftFromEntity);
        $('#entitySelect').on('change', onEntitySelected);
        $('#btnAddColumn').on('click', () => addColumnRow());
        $('#btnAddFilter').on('click', () => addFilterRow());
        $('#btnAddGroup').on('click', () => addGroupRow());
        $('#btnAddSummary').on('click', () => addSummaryRow());
        $('#btnAddSignature').on('click', () => addSignatureRow());
        $('#btnSaveReport').on('click', saveReport);
        $('#showSignatureBlock').on('change', updateSignatureSectionUi);

        $(document).on('click', '.btn-remove-row', function () {
            $(this).closest('tr').remove();
        });

        $(document).on('click', '.btn-move-up', function () {
            const $row = $(this).closest('tr');
            const $prev = $row.prev();
            if ($prev.length) $row.insertBefore($prev);
        });

        $(document).on('click', '.btn-move-down', function () {
            const $row = $(this).closest('tr');
            const $next = $row.next();
            if ($next.length) $row.insertAfter($next);
        });

        $(document).on('change', '.col-role', function () {
            updateColumnRoleUi($(this).closest('tr'));
        });

        $(document).on('change', '.flt-control', function () {
            updateFilterControlUi($(this).closest('tr'));
        });

        bindReportTypeOptions();
    }

    function updateColumnRoleUi($row) {
        const role = $row.find('.col-role').val();
        const isCalculated = role === 'Calculated';
        const isAggregate = role === 'Aggregate';
        $row.find('.col-formula').prop('disabled', !isCalculated).toggleClass('bg-light', !isCalculated);
        $row.find('.col-agg').prop('disabled', isCalculated).toggleClass('bg-light', isCalculated);
        if (isCalculated)
            $row.find('.col-agg').val('None');
        if (!isAggregate && !isCalculated)
            $row.find('.col-agg').val('None');
    }

    function updateFilterControlUi($row) {
        const controlType = $row.find('.flt-control').val();
        const isDropdown = controlType === 'Dropdown';
        const isAutocomplete = controlType === 'Autocomplete';
        const isLookupControl = isDropdown || isAutocomplete;
        const isDateRange = controlType === 'DateRange';
        $row.find('.flt-lookup').prop('disabled', !isLookupControl).toggleClass('bg-light', !isLookupControl);
        $row.find('.flt-options').prop('disabled', !isDropdown).toggleClass('bg-light', !isDropdown);
        $row.find('.flt-op').prop('disabled', isDateRange).toggleClass('bg-light', isDateRange);
        if (isDateRange)
            $row.find('.flt-op').val('Between');
        if (isAutocomplete && $row.find('.flt-op').val() === 'Contains')
            $row.find('.flt-op').val('Equals');
    }

    function bindReportTypeOptions() {
        $('.form-builder-type-option').on('click', function () {
            const type = $(this).data('type');
            $('.form-builder-type-option').removeClass('is-selected');
            $(this).addClass('is-selected');
            $(this).find('input[type=radio]').prop('checked', true);
            state.reportType = type;
            updateReportTypeUi();
        });
    }

    function updateReportTypeUi() {
        state.reportType = $('input[name=reportTypeOption]:checked').val() || state.reportType;
        const isGrouped = state.reportType === 'Grouped';
        const isSummary = state.reportType === 'Summary';

        $('.nav-grouping-tab').toggleClass('d-none', !isGrouped && !isSummary);
        $('#tabGroups').closest('.tab-pane').toggleClass('d-none', !isGrouped && !isSummary);
    }

    function onEntitySelected() {
        const entity = $('#entitySelect').val();
        $('#entityName').val(entity || '');
        loadPropertyPaths(entity);
        if (!state.isEdit && entity) {
            const slug = entity.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
            if (!$('#reportCode').val()) $('#reportCode').val(`${slug}-report`);
            if (!$('#reportName').val()) $('#reportName').val(`${entity.replace(/([A-Z])/g, ' $1').trim()} Report`);
        }
    }

    async function loadPropertyPaths(entityName) {
        if (!entityName) {
            state.propertyPaths = [];
            refreshPropertyDatalist();
            return;
        }

        try {
            state.propertyPaths = await $.getJSON(
                `/api/metaforge/reportconfig/properties/${encodeURIComponent(entityName)}`);
        } catch {
            state.propertyPaths = [];
        }

        refreshPropertyDatalist();
    }

    function refreshPropertyDatalist() {
        const options = (state.propertyPaths || []).map(p => {
            const path = pick(p, 'Path', 'path', '');
            const label = pick(p, 'Label', 'label', path);
            return `<option value="${escapeAttr(path)}">${escapeAttr(label)}</option>`;
        }).join('');

        $('#reportPropertyPathsList').html(options);
    }

    function propertyPathInput(className, value, placeholder) {
        return `<input type="text" class="form-control form-control-sm ${className}" list="reportPropertyPathsList" value="${escapeAttr(value)}" placeholder="${escapeAttr(placeholder || 'PropertyName')}" />`;
    }

    async function loadDraftFromEntity() {
        const entity = $('#entitySelect').val();
        if (!entity) {
            notify('Select an entity first.');
            return;
        }

        try {
            const groupName = $('#groupName').val() || 'Reports';
            const draft = await $.getJSON(`/api/metaforge/reportconfig/draft/${encodeURIComponent(entity)}?groupName=${encodeURIComponent(groupName)}`);
            loadReport(draft, true);
            notify('Draft generated from entity metadata.', 'success');
        } catch (xhr) {
            notify(xhr.responseJSON?.message || xhr.responseJSON?.title || 'Could not build draft.');
        }
    }

    function loadReport(report, preserveIdentity) {
        state.reportId = pick(report, 'Id', 'id', 0);
        state.reportType = pick(report, 'ReportType', 'reportType', 'Tabular');

        if (!preserveIdentity) {
            const entityName = pick(report, 'EntityName', 'entityName', '');
            $('#entitySelect').val(entityName);
            $('#entityName').val(entityName);
            $('#groupName').val(pick(report, 'GroupName', 'groupName', 'Reports'));
            $('#reportCode').val(pick(report, 'Code', 'code', ''));
            $('#reportName').val(pick(report, 'Name', 'name', ''));
            $('#description').val(pick(report, 'Description', 'description', '') || '');
            $('#displayOrder').val(pick(report, 'DisplayOrder', 'displayOrder', 0));
            $('#isActive').prop('checked', pick(report, 'IsActive', 'isActive', true) !== false);
            $('#exportTitle').val(pick(report, 'ExportTitle', 'exportTitle', '') || '');
            $('#showTitleUnderline').prop('checked', pick(report, 'ShowTitleUnderline', 'showTitleUnderline', true) !== false);
            $('#showSignatureBlock').prop('checked', pick(report, 'ShowSignatureBlock', 'showSignatureBlock', false) === true);
            $('#showGeneratedTimestamp').prop('checked', pick(report, 'ShowGeneratedTimestamp', 'showGeneratedTimestamp', true) !== false);
            $('#showPageNumbers').prop('checked', pick(report, 'ShowPageNumbers', 'showPageNumbers', true) !== false);
            $('#headerLeft').val(pick(report, 'HeaderLeft', 'headerLeft', '') || '');
            $('#headerCenter').val(pick(report, 'HeaderCenter', 'headerCenter', '') || '');
            $('#headerRight').val(pick(report, 'HeaderRight', 'headerRight', '') || '');
            $('#footerLeft').val(pick(report, 'FooterLeft', 'footerLeft', '') || '');
            $('#footerCenter').val(pick(report, 'FooterCenter', 'footerCenter', '') || '');
            $('#footerRight').val(pick(report, 'FooterRight', 'footerRight', '') || '');
        }

        $(`.form-builder-type-option[data-type="${state.reportType}"]`).trigger('click');

        $('#columnsTable tbody').empty();
        (pick(report, 'Columns', 'columns', []) || []).forEach(c => addColumnRow(c));

        $('#filtersTable tbody').empty();
        (pick(report, 'Filters', 'filters', []) || []).forEach(f => addFilterRow(f));

        $('#groupsTable tbody').empty();
        (pick(report, 'Groups', 'groups', []) || []).forEach(g => addGroupRow(g));

        $('#summariesTable tbody').empty();
        (pick(report, 'Summaries', 'summaries', []) || []).forEach(s => addSummaryRow(s));

        $('#signaturesTable tbody').empty();
        (pick(report, 'Signatures', 'signatures', []) || []).forEach(s => addSignatureRow(s));

        updateSignatureSectionUi();
        updateReportTypeUi();

        const entityName = pick(report, 'EntityName', 'entityName', '') || $('#entityName').val();
        loadPropertyPaths(entityName);
    }

    function updateSignatureSectionUi() {
        const enabled = $('#showSignatureBlock').is(':checked');
        $('#signatureSection').toggleClass('opacity-50', !enabled);
        $('#btnAddSignature').prop('disabled', !enabled);
        $('#signaturesTable .sig-label').prop('disabled', !enabled);
    }

    function rowActionsHtml() {
        return `<td class="text-nowrap">
            <button type="button" class="btn btn-sm btn-outline-secondary btn-icon btn-move-up" title="Move up"><i class="fa-solid fa-arrow-up"></i></button>
            <button type="button" class="btn btn-sm btn-outline-secondary btn-icon btn-move-down" title="Move down"><i class="fa-solid fa-arrow-down"></i></button>
            <button type="button" class="btn btn-sm btn-outline-danger btn-icon btn-remove-row" title="Remove"><i class="fa-solid fa-trash"></i></button>
        </td>`;
    }

    function selectOptions(values, selected) {
        return values.map(v => `<option value="${v}"${v === selected ? ' selected' : ''}>${v}</option>`).join('');
    }

    function addColumnRow(data) {
        data = data || {};
        const role = pick(data, 'ColumnRole', 'columnRole', 'Detail');
        const agg = pick(data, 'AggregateFunction', 'aggregateFunction', 'None');
        const visible = pick(data, 'IsVisible', 'isVisible', true) !== false;
        const prop = pick(data, 'PropertyName', 'propertyName', '');
        const label = pick(data, 'Label', 'label', '');
        const formula = pick(data, 'Formula', 'formula', '') || '';
        const displayFormat = pick(data, 'DisplayFormat', 'displayFormat', '') || '';
        const $row = $(`<tr>
            <td>${propertyPathInput('col-prop', prop, 'SalesOrder.Customer.Name')}</td>
            <td><input type="text" class="form-control form-control-sm col-label" value="${escapeAttr(label)}" placeholder="Label" /></td>
            <td><select class="form-select form-select-sm col-role">${selectOptions(COLUMN_ROLES, role)}</select></td>
            <td><input type="text" class="form-control form-control-sm col-formula" value="${escapeAttr(formula)}" placeholder="{Quantity} * {UnitPrice}" title="Use {PropertyName} tokens" /></td>
            <td><input type="text" class="form-control form-control-sm col-format" value="${escapeAttr(displayFormat)}" placeholder="N2" /></td>
            <td><select class="form-select form-select-sm col-agg">${selectOptions(AGGREGATE_FUNCTIONS, agg)}</select></td>
            <td class="text-center"><input type="checkbox" class="form-check-input col-visible" ${visible ? 'checked' : ''} /></td>
            ${rowActionsHtml()}
        </tr>`);
        $('#columnsTable tbody').append($row);
        updateColumnRoleUi($row);
    }

    function addFilterRow(data) {
        data = data || {};
        const op = pick(data, 'Operator', 'operator', 'Equals');
        const controlType = pick(data, 'ControlType', 'controlType', 'TextBox');
        const required = pick(data, 'IsRequired', 'isRequired', false) === true;
        const prop = pick(data, 'PropertyName', 'propertyName', '');
        const label = pick(data, 'Label', 'label', '');
        const defaultValue = pick(data, 'DefaultValue', 'defaultValue', '') || '';
        const lookupEntity = pick(data, 'LookupEntity', 'lookupEntity', '') || '';
        const options = pick(data, 'Options', 'options', '') || '';
        const $row = $(`<tr>
            <td>${propertyPathInput('flt-prop', prop)}</td>
            <td><input type="text" class="form-control form-control-sm flt-label" value="${escapeAttr(label)}" placeholder="Label" /></td>
            <td><select class="form-select form-select-sm flt-control">${selectOptions(FILTER_CONTROLS, controlType)}</select></td>
            <td><select class="form-select form-select-sm flt-op">${selectOptions(FILTER_OPERATORS, op)}</select></td>
            <td><input type="text" class="form-control form-control-sm flt-lookup" value="${escapeAttr(lookupEntity)}" placeholder="Customer" title="Lookup entity (required for Autocomplete)" /></td>
            <td><input type="text" class="form-control form-control-sm flt-options" value="${escapeAttr(options)}" placeholder="Active,Inactive" title="Static dropdown options" /></td>
            <td><input type="text" class="form-control form-control-sm flt-default" value="${escapeAttr(defaultValue)}" placeholder="Default (from|to for date range)" /></td>
            <td class="text-center"><input type="checkbox" class="form-check-input flt-required" ${required ? 'checked' : ''} /></td>
            ${rowActionsHtml()}
        </tr>`);
        $('#filtersTable tbody').append($row);
        updateFilterControlUi($row);
    }

    function addGroupRow(data) {
        data = data || {};
        const prop = pick(data, 'PropertyName', 'propertyName', '');
        const label = pick(data, 'Label', 'label', '');
        const sortDescending = pick(data, 'SortDescending', 'sortDescending', false) === true;
        const showHeader = pick(data, 'ShowGroupHeader', 'showGroupHeader', true) !== false;
        const showSubtotal = pick(data, 'ShowSubtotal', 'showSubtotal', true) !== false;
        $('#groupsTable tbody').append(`<tr>
            <td>${propertyPathInput('grp-prop', prop)}</td>
            <td><input type="text" class="form-control form-control-sm grp-label" value="${escapeAttr(label)}" placeholder="Label" /></td>
            <td class="text-center"><input type="checkbox" class="form-check-input grp-desc" ${sortDescending ? 'checked' : ''} /></td>
            <td class="text-center"><input type="checkbox" class="form-check-input grp-header" ${showHeader ? 'checked' : ''} /></td>
            <td class="text-center"><input type="checkbox" class="form-check-input grp-subtotal" ${showSubtotal ? 'checked' : ''} /></td>
            ${rowActionsHtml()}
        </tr>`);
    }

    function addSummaryRow(data) {
        data = data || {};
        const agg = pick(data, 'AggregateFunction', 'aggregateFunction', 'Sum');
        const prop = pick(data, 'PropertyName', 'propertyName', '');
        const label = pick(data, 'Label', 'label', '');
        $('#summariesTable tbody').append(`<tr>
            <td>${propertyPathInput('sum-prop', prop)}</td>
            <td><input type="text" class="form-control form-control-sm sum-label" value="${escapeAttr(label)}" placeholder="Label" /></td>
            <td><select class="form-select form-select-sm sum-agg">${selectOptions(AGGREGATE_FUNCTIONS.filter(a => a !== 'None'), agg)}</select></td>
            ${rowActionsHtml()}
        </tr>`);
    }

    function addSignatureRow(data) {
        data = data || {};
        const label = pick(data, 'Label', 'label', '');
        $('#signaturesTable tbody').append(`<tr>
            <td><input type="text" class="form-control form-control-sm sig-label" value="${escapeAttr(label)}" placeholder="Prepared By" /></td>
            ${rowActionsHtml()}
        </tr>`);
        updateSignatureSectionUi();
    }

    function collectColumns() {
        const rows = [];
        $('#columnsTable tbody tr').each(function (i) {
            const prop = $(this).find('.col-prop').val()?.trim();
            if (!prop) return;
            rows.push({
                PropertyName: prop,
                Label: $(this).find('.col-label').val()?.trim() || prop,
                DisplayOrder: i,
                IsVisible: $(this).find('.col-visible').is(':checked'),
                ColumnRole: $(this).find('.col-role').val(),
                Formula: $(this).find('.col-formula').val()?.trim() || null,
                DisplayFormat: $(this).find('.col-format').val()?.trim() || null,
                AggregateFunction: $(this).find('.col-agg').val()
            });
        });
        return rows;
    }

    function collectFilters() {
        const rows = [];
        $('#filtersTable tbody tr').each(function (i) {
            const prop = $(this).find('.flt-prop').val()?.trim();
            if (!prop) return;
            rows.push({
                PropertyName: prop,
                Label: $(this).find('.flt-label').val()?.trim() || prop,
                ControlType: $(this).find('.flt-control').val(),
                Operator: $(this).find('.flt-op').val(),
                LookupEntity: $(this).find('.flt-lookup').val()?.trim() || null,
                Options: $(this).find('.flt-options').val()?.trim() || null,
                DefaultValue: $(this).find('.flt-default').val()?.trim() || null,
                IsRequired: $(this).find('.flt-required').is(':checked'),
                DisplayOrder: i
            });
        });
        return rows;
    }

    function collectGroups() {
        const rows = [];
        $('#groupsTable tbody tr').each(function (i) {
            const prop = $(this).find('.grp-prop').val()?.trim();
            if (!prop) return;
            rows.push({
                PropertyName: prop,
                Label: $(this).find('.grp-label').val()?.trim() || prop,
                DisplayOrder: i,
                SortDescending: $(this).find('.grp-desc').is(':checked'),
                ShowGroupHeader: $(this).find('.grp-header').is(':checked'),
                ShowSubtotal: $(this).find('.grp-subtotal').is(':checked')
            });
        });
        return rows;
    }

    function collectSummaries() {
        const rows = [];
        $('#summariesTable tbody tr').each(function (i) {
            const prop = $(this).find('.sum-prop').val()?.trim();
            if (!prop) return;
            rows.push({
                PropertyName: prop,
                Label: $(this).find('.sum-label').val()?.trim() || prop,
                AggregateFunction: $(this).find('.sum-agg').val(),
                DisplayOrder: i
            });
        });
        return rows;
    }

    function collectSignatures() {
        const rows = [];
        $('#signaturesTable tbody tr').each(function (i) {
            const label = $(this).find('.sig-label').val()?.trim();
            if (!label) return;
            rows.push({
                Label: label,
                DisplayOrder: i
            });
        });
        return rows;
    }

    function buildPayload() {
        const entity = state.isEdit
            ? $('#entityName').val()?.trim()
            : $('#entitySelect').val()?.trim();

        return {
            Id: state.reportId,
            Code: $('#reportCode').val()?.trim(),
            Name: $('#reportName').val()?.trim(),
            EntityName: entity,
            GroupName: $('#groupName').val()?.trim() || 'Reports',
            ReportType: $('input[name=reportTypeOption]:checked').val() || 'Tabular',
            DisplayOrder: parseInt($('#displayOrder').val(), 10) || 0,
            IsActive: $('#isActive').is(':checked'),
            Description: $('#description').val()?.trim() || null,
            ExportTitle: $('#exportTitle').val()?.trim() || null,
            ShowTitleUnderline: $('#showTitleUnderline').is(':checked'),
            ShowSignatureBlock: $('#showSignatureBlock').is(':checked'),
            ShowGeneratedTimestamp: $('#showGeneratedTimestamp').is(':checked'),
            ShowPageNumbers: $('#showPageNumbers').is(':checked'),
            HeaderLeft: $('#headerLeft').val()?.trim() || null,
            HeaderCenter: $('#headerCenter').val()?.trim() || null,
            HeaderRight: $('#headerRight').val()?.trim() || null,
            FooterLeft: $('#footerLeft').val()?.trim() || null,
            FooterCenter: $('#footerCenter').val()?.trim() || null,
            FooterRight: $('#footerRight').val()?.trim() || null,
            Columns: collectColumns(),
            Filters: collectFilters(),
            Groups: collectGroups(),
            Summaries: collectSummaries(),
            Signatures: collectSignatures()
        };
    }

    async function saveReport() {
        const payload = buildPayload();

        if (!payload.EntityName) {
            notify('Select an entity.');
            return;
        }
        if (!payload.Code || !payload.Name) {
            notify('Report code and name are required.');
            return;
        }
        if (payload.Columns.length === 0) {
            notify('Add at least one column.');
            return;
        }
        if (payload.ShowSignatureBlock && payload.Signatures.length === 0) {
            notify('Add at least one signature line or disable the signature block.');
            return;
        }

        try {
            const result = await $.ajax({
                url: '/api/metaforge/reportconfig',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload)
            });
            notify('Report saved successfully.', 'success');
            window.location.href = result.url || result.Url || '/ReportBuilder';
        } catch (xhr) {
            notify(xhr.responseJSON?.message || xhr.responseJSON?.title || 'Save failed.');
        }
    }

    function escapeAttr(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;');
    }

    return { init };
})();

$(function () {
    if ($('#reportBuilderApp').length) {
        ReportBuilder.init();
    }
});
