/**
 * Dynamic Report runtime — tabular, grouped, and summary reports with filters and Excel export.
 */
const DynamicReport = (function () {
    let table;
    let reportCode;
    let definition;
    let permissions = {};

    const FILTER_CONTROLS = ['TextBox', 'Dropdown', 'Autocomplete', 'DateRange'];

    function pick(obj, pascal, camel, fallback) {
        if (obj == null) return fallback;
        if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
        if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
        return fallback;
    }

    function getColumns(def) {
        return (def?.Columns ?? def?.columns ?? []).filter(c => c.IsVisible !== false && c.isVisible !== false);
    }

    function getFilters(def) {
        return def?.Filters ?? def?.filters ?? [];
    }

    function escapeAttr(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;');
    }

    function sanitizeId(propertyName) {
        return String(propertyName).replace(/[^a-zA-Z0-9_-]/g, '_');
    }

    function getRowValue(row, propertyName) {
        if (row == null || !propertyName)
            return null;

        if (Object.prototype.hasOwnProperty.call(row, propertyName))
            return row[propertyName];

        return null;
    }

    function isNumericDisplayFormat(displayFormat) {
        const fmt = String(displayFormat ?? '').trim().toUpperCase();
        if (!fmt)
            return false;

        return /^[NCPF](\d+)?$/.test(fmt) || fmt === '0.00' || fmt === '#,##0.00';
    }

    function formatCellValue(value, displayFormat, controlType) {
        if (value == null || value === '')
            return '';

        if (typeof MetaForgeGridDisplayFormat !== 'undefined') {
            const temporal = MetaForgeGridDisplayFormat.isTemporalControlType(controlType)
                || MetaForgeGridDisplayFormat.resolveFormatKey(displayFormat, controlType);
            if (temporal) {
                const formatted = MetaForgeGridDisplayFormat.formatValue(value, displayFormat, controlType);
                if (formatted !== '')
                    return formatted;
            }
        }

        const fmt = String(displayFormat ?? '').trim();
        if (fmt && isNumericDisplayFormat(fmt)) {
            const num = Number(value);
            if (!Number.isNaN(num)) {
                const match = fmt.match(/^([NCPF])(\d+)?$/i);
                if (match) {
                    const decimals = match[2] != null ? parseInt(match[2], 10) : (match[1].toUpperCase() === 'N' ? 0 : 2);
                    if (match[1].toUpperCase() === 'C')
                        return num.toLocaleString(undefined, { style: 'currency', currency: 'USD', minimumFractionDigits: decimals, maximumFractionDigits: decimals });
                    if (match[1].toUpperCase() === 'P')
                        return num.toLocaleString(undefined, { style: 'percent', minimumFractionDigits: decimals, maximumFractionDigits: decimals });
                    return num.toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
                }
            }
        }

        if (typeof value === 'object')
            return JSON.stringify(value);

        return String(value);
    }

    function parseDefaultDateRange(defaultValue) {
        if (!defaultValue || !String(defaultValue).includes('|'))
            return { from: '', to: '' };

        const parts = String(defaultValue).split('|', 2);
        return { from: parts[0]?.trim() ?? '', to: parts[1]?.trim() ?? '' };
    }

    function renderFilterPanel() {
        const filters = getFilters(definition);
        const $section = $('#reportFilterSection');
        const $panel = $('#reportFilterPanel');
        $panel.empty();

        if (!filters.length) {
            $section.addClass('d-none');
            return;
        }

        $section.removeClass('d-none');

        filters.forEach(filter => {
            const property = pick(filter, 'PropertyName', 'propertyName', '');
            const label = pick(filter, 'Label', 'label', property);
            const controlType = pick(filter, 'ControlType', 'controlType', 'TextBox');
            const operator = pick(filter, 'Operator', 'operator', 'Equals');
            const defaultValue = pick(filter, 'DefaultValue', 'defaultValue', '') ?? '';
            const isRequired = pick(filter, 'IsRequired', 'isRequired', false) === true;
            const lookupEntity = pick(filter, 'LookupEntity', 'lookupEntity', '');
            const options = pick(filter, 'Options', 'options', '');
            const safeId = sanitizeId(property);

            let controlHtml = '';
            if (controlType === 'DateRange') {
                const range = parseDefaultDateRange(defaultValue);
                controlHtml = `
                    <div class="row g-2 report-filter-daterange" data-property="${escapeAttr(property)}" data-control="DateRange">
                        <div class="col-6">
                            <input type="date" class="form-control form-control-sm report-filter-range-from"
                                   data-property="${escapeAttr(property)}" data-range-part="from"
                                   value="${escapeAttr(range.from)}" aria-label="${escapeAttr(label)} from" />
                        </div>
                        <div class="col-6">
                            <input type="date" class="form-control form-control-sm report-filter-range-to"
                                   data-property="${escapeAttr(property)}" data-range-part="to"
                                   value="${escapeAttr(range.to)}" aria-label="${escapeAttr(label)} to" />
                        </div>
                    </div>`;
            } else if (controlType === 'Dropdown') {
                controlHtml = `
                    <select class="form-select form-select-sm report-filter report-filter-dropdown"
                            id="filter_${safeId}"
                            data-property="${escapeAttr(property)}"
                            data-control="Dropdown"
                            data-operator="${escapeAttr(operator)}"
                            data-lookup="${escapeAttr(lookupEntity)}"
                            data-options="${escapeAttr(options)}">
                        <option value="">— All —</option>
                    </select>`;
            } else if (controlType === 'Autocomplete') {
                controlHtml = `
                    <select class="form-select form-select-sm report-filter report-filter-autocomplete lookup-autocomplete"
                            id="filter_${safeId}"
                            data-property="${escapeAttr(property)}"
                            data-control="Autocomplete"
                            data-operator="${escapeAttr(operator)}"
                            data-lookup="${escapeAttr(lookupEntity)}">
                        <option value=""></option>
                    </select>`;
            } else {
                const inputType = operator === 'GreaterOrEqual' || operator === 'LessOrEqual' ? 'date' : 'text';
                controlHtml = `
                    <input type="${inputType}"
                           class="form-control form-control-sm report-filter"
                           id="filter_${safeId}"
                           data-property="${escapeAttr(property)}"
                           data-control="TextBox"
                           data-operator="${escapeAttr(operator)}"
                           value="${escapeAttr(defaultValue)}"
                           placeholder="${operator === 'Contains' ? 'Search…' : ''}" />`;
            }

            $panel.append(`
                <div class="col-md-6 col-lg-4 col-xl-3 report-filter-field">
                    <label class="form-label" for="filter_${safeId}">
                        ${escapeAttr(label)}${isRequired ? ' <span class="required-mark">*</span>' : ''}
                    </label>
                    ${controlHtml}
                </div>`);
        });

        initializeLookupFilters();
    }

    function resolveLookupEntity(property, lookupEntity) {
        if (lookupEntity)
            return lookupEntity.toString().trim();

        const leaf = String(property).split('.').pop() ?? property;
        if (leaf.endsWith('Id') && leaf.toLowerCase() !== 'id')
            return leaf.slice(0, -2);

        return '';
    }

    function initializeLookupFilters() {
        initializeDropdownFilters();
        initializeAutocompleteFilters();
    }

    function initializeDropdownFilters() {
        $('#reportFilterPanel .report-filter-dropdown').each(function () {
            const $select = $(this);
            const lookup = ($select.data('lookup') || '').toString().trim();
            const options = ($select.data('options') || '').toString().trim();
            const defaultValue = getFilters(definition).find(f =>
                pick(f, 'PropertyName', 'propertyName', '') === $select.data('property'));
            const selected = pick(defaultValue, 'DefaultValue', 'defaultValue', '');

            if (options) {
                options.split(',').map(v => v.trim()).filter(Boolean).forEach(value => {
                    const $opt = $('<option>').val(value).text(value);
                    if (selected && String(selected).toLowerCase() === value.toLowerCase())
                        $opt.prop('selected', true);
                    $select.append($opt);
                });
                return;
            }

            if (!lookup)
                return;

            $.getJSON(`/api/metaforge/lookups/${encodeURIComponent(lookup)}`)
                .done(items => {
                    (items || []).forEach(item => {
                        const value = item.value ?? item.Value ?? item.id ?? item.Id;
                        const text = item.text ?? item.Text ?? item.name ?? item.Name ?? value;
                        const $opt = $('<option>').val(value).text(text);
                        if (selected && String(selected) === String(value))
                            $opt.prop('selected', true);
                        $select.append($opt);
                    });
                })
                .fail(() => {
                    if (typeof MetaForgeUi !== 'undefined')
                        MetaForgeUi.showAlert(`Could not load lookup options for ${lookup}.`, 'warning');
                });
        });
    }

    function initializeAutocompleteFilters() {
        if (typeof MetaForgeLookups === 'undefined')
            return;

        $('#reportFilterPanel .report-filter-autocomplete').each(function () {
            const $select = $(this);
            const property = $select.data('property');
            const lookup = resolveLookupEntity(property, ($select.data('lookup') || '').toString());
            const filterDef = getFilters(definition).find(f =>
                pick(f, 'PropertyName', 'propertyName', '') === property);
            const selected = pick(filterDef, 'DefaultValue', 'defaultValue', '');

            if (!lookup) {
                if (typeof MetaForgeUi !== 'undefined')
                    MetaForgeUi.showAlert(`Autocomplete filter '${property}' is missing Lookup Entity.`, 'warning');
                return;
            }

            MetaForgeLookups.initPagedLookupSelect($select, lookup, {
                selectedValue: selected || null,
                placeholder: '— Search —'
            });
        });
    }

    function collectFilterValues() {
        const values = {};
        const handledDateRanges = new Set();

        $('#reportFilterPanel .report-filter-dropdown, #reportFilterPanel .report-filter-autocomplete, #reportFilterPanel .report-filter[data-control="TextBox"]').each(function () {
            const property = $(this).data('property');
            const value = ($(this).val() ?? '').toString().trim();
            if (property && value.length > 0)
                values[property] = value;
        });

        $('#reportFilterPanel .report-filter-daterange').each(function () {
            const property = $(this).data('property');
            if (!property || handledDateRanges.has(property))
                return;

            handledDateRanges.add(property);
            const from = $(this).find('.report-filter-range-from').val()?.toString().trim() ?? '';
            const to = $(this).find('.report-filter-range-to').val()?.toString().trim() ?? '';
            if (from || to)
                values[property] = `${from}|${to}`;
        });

        return values;
    }

    function resetFilters() {
        getFilters(definition).forEach(filter => {
            const property = pick(filter, 'PropertyName', 'propertyName', '');
            const controlType = pick(filter, 'ControlType', 'controlType', 'TextBox');
            const defaultValue = pick(filter, 'DefaultValue', 'defaultValue', '') ?? '';
            const safeId = sanitizeId(property);

            if (controlType === 'DateRange') {
                const range = parseDefaultDateRange(defaultValue);
                const $wrap = $(`.report-filter-daterange[data-property="${property}"]`);
                $wrap.find('.report-filter-range-from').val(range.from);
                $wrap.find('.report-filter-range-to').val(range.to);
                return;
            }

            const $input = $(`#filter_${safeId}`);
            if (controlType === 'Autocomplete' && $input.hasClass('select2-hidden-accessible')) {
                $input.val(defaultValue || null).trigger('change');
                return;
            }

            $input.val(defaultValue);
        });
    }

    function buildExportUrl(format) {
        const params = new URLSearchParams();
        params.set('Page', '1');
        params.set('PageSize', '10000');
        params.set('ExportAll', 'true');

        const search = table?.search()?.trim();
        if (search)
            params.set('SearchTerm', search);

        const order = table?.order();
        const columns = getColumns(definition);
        if (order?.length && columns[order[0].column]) {
            const col = columns[order[0].column];
            params.set('SortColumn', col.PropertyName ?? col.propertyName ?? '');
            params.set('SortDescending', order[0].dir === 'desc');
        }

        const filters = collectFilterValues();
        Object.keys(filters).forEach(key => params.set(`FilterValues[${key}]`, filters[key]));

        const path = format === 'pdf' ? 'export/pdf' : 'export/excel';
        return `/api/metaforge/reports/${encodeURIComponent(reportCode)}/${path}?${params.toString()}`;
    }

    function mapResponseRows(response) {
        const rows = response.Rows ?? response.rows ?? [];
        return rows.map(r => {
            const values = r.Values ?? r.values ?? {};
            return {
                ...values,
                __rowType: r.RowType ?? r.rowType ?? 'Detail',
                __label: r.Label ?? r.label ?? '',
                __level: r.Level ?? r.level ?? 0
            };
        });
    }

    function rowClassForType(rowType) {
        switch (rowType) {
            case 'GroupHeader': return 'report-row-group-header';
            case 'GroupSubtotal': return 'report-row-subtotal';
            case 'GrandTotal': return 'report-row-grand-total';
            case 'Summary': return 'report-row-summary';
            default: return '';
        }
    }

    function setRunningState(isRunning) {
        $('.report-preview').toggleClass('is-running', isRunning);
        const $stat = $('#reportRecordStat');
        $stat.toggleClass('is-loading', isRunning);
        if (isRunning)
            $stat.text('Loading…');
    }

    function updateRecordStat(total, page, pageSize) {
        const $stat = $('#reportRecordStat');
        if (!$stat.length)
            return;

        $stat.removeClass('is-loading');
        const count = Number(total) || 0;
        if (count === 0) {
            $stat.text('No rows');
            return;
        }

        const start = (page - 1) * pageSize + 1;
        const end = Math.min(page * pageSize, count);
        $stat.text(count > pageSize
            ? `${start.toLocaleString()}–${end.toLocaleString()} of ${count.toLocaleString()}`
            : `${count.toLocaleString()} row${count === 1 ? '' : 's'}`);
    }

    function initGrid() {
        const columns = getColumns(definition);
        const $table = $('#reportGrid');
        const isGrouped = !['Tabular', 'tabular'].includes(definition?.ReportType ?? definition?.reportType ?? 'Tabular');

        if (table) {
            table.destroy();
            $table.find('tbody').remove();
        }

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
                    Page: page,
                    PageSize: pageSize,
                    SearchTerm: data.search?.value || '',
                    SortColumn: sortCol?.PropertyName ?? sortCol?.propertyName ?? null,
                    SortDescending: data.order?.length ? data.order[0].dir === 'desc' : false,
                    FilterValues: collectFilterValues()
                };

                setRunningState(true);

                $.ajax({
                    url: `/api/metaforge/reports/${encodeURIComponent(reportCode)}/data`,
                    method: 'POST',
                    contentType: 'application/json',
                    metaforgeProgress: false,
                    data: JSON.stringify(request),
                    success: function (response) {
                        const total = response.TotalCount ?? response.totalCount ?? 0;
                        updateRecordStat(total, page, pageSize);
                        callback({
                            draw: data.draw,
                            recordsTotal: total,
                            recordsFiltered: total,
                            data: mapResponseRows(response)
                        });
                    },
                    error: function (xhr) {
                        setRunningState(false);
                        updateRecordStat(0, 1, pageSize);
                        const message = xhr.responseJSON?.message || xhr.responseJSON?.title || 'Could not load report data.';
                        if (typeof MetaForgeUi !== 'undefined')
                            MetaForgeUi.showAlert(message, 'danger');
                        callback({ draw: data.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
                    },
                    complete: function () {
                        setRunningState(false);
                    }
                });
            },
            columns: columns.map((col, colIndex) => {
                const property = col.PropertyName ?? col.propertyName ?? '';
                const label = col.Label ?? col.label ?? property;
                const displayFormat = col.DisplayFormat ?? col.displayFormat ?? '';
                const controlType = col.ControlType ?? col.controlType ?? '';
                const numeric = isNumericDisplayFormat(displayFormat);

                return {
                    title: label,
                    data: function (row) {
                        return getRowValue(row, property);
                    },
                    name: property,
                    orderable: !isGrouped && col.IsSortable !== false && col.isSortable !== false,
                    className: numeric ? 'text-end' : '',
                    defaultContent: '',
                    render: function (value, type, row) {
                        const rowType = row.__rowType ?? 'Detail';
                        const groupLabel = row.__label ?? '';

                        if (colIndex === 0 && groupLabel && rowType !== 'Detail')
                            return $('<span>').text(groupLabel).html();

                        if (type !== 'display' && type !== 'filter')
                            return value;

                        return formatCellValue(value, displayFormat, controlType);
                    }
                };
            }),
            order: isGrouped ? [] : [[0, 'asc']],
            ordering: !isGrouped,
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            language: {
                search: '',
                searchPlaceholder: 'Search in results…',
                lengthMenu: 'Show _MENU_',
                info: 'Showing _START_–_END_ of _TOTAL_',
                infoEmpty: 'No matching rows',
                zeroRecords: 'No data for the current filters',
                paginate: {
                    first: '«',
                    last: '»',
                    next: '›',
                    previous: '‹'
                }
            },
            createdRow: function (row, data) {
                const cssClass = rowClassForType(data.__rowType);
                if (cssClass)
                    $(row).addClass(cssClass);
                if (data.__level > 0 && data.__rowType === 'Detail')
                    $(row).find('td:first').css('padding-left', `${(data.__level + 1) * 12}px`);
            }
        });
    }

    function bindEvents() {
        $('#btnApplyFilters').on('click', function () {
            if (table) table.ajax.reload();
        });

        $('#btnResetFilters').on('click', function () {
            resetFilters();
            if (table) table.ajax.reload();
        });

        $('#btnExportExcel').on('click', function () {
            window.location.href = buildExportUrl('excel');
        });

        $('#btnExportPdf').on('click', function () {
            window.location.href = buildExportUrl('pdf');
        });

        $('#reportFilterPanel').on('keydown', '.report-filter, .report-filter-range-from, .report-filter-range-to', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                if (table) table.ajax.reload();
            }
        });
    }

    function init() {
        const runtime = window.__reportRuntime || {};
        reportCode = runtime.reportCode;
        definition = runtime.definition || {};
        permissions = runtime.permissions || {};

        if (!reportCode || !$('#reportGrid').length)
            return;

        renderFilterPanel();
        bindEvents();
        initGrid();
    }

    return { init };
})();

$(function () {
    DynamicReport.init();
});
