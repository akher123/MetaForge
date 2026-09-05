using MetaForge.Application.Configuration;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Reports;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Executes tabular, grouped, and summary dynamic reports against configured entities.
/// </summary>
public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IFormMetadataCache _formCache;
    private readonly ILookupService _lookupService;
    private readonly ExportOptions _exportOptions;

    public ReportService(
        IUnitOfWork unitOfWork,
        MetaForgeDbContext dbContext,
        IEntityTypeResolver typeResolver,
        IFormMetadataCache formCache,
        ILookupService lookupService,
        IOptions<ExportOptions> exportOptions)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _typeResolver = typeResolver;
        _formCache = formCache;
        _lookupService = lookupService;
        _exportOptions = exportOptions.Value;
    }

    public async Task<ReportDefinitionDto?> GetDefinitionAsync(string reportCode, CancellationToken cancellationToken = default)
    {
        var report = await LoadActiveReportAsync(reportCode, cancellationToken);
        if (report == null) return null;

        var form = await _formCache.GetByEntityNameAsync(report.EntityName, cancellationToken);
        return MapDefinition(report, form);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportCode,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await LoadActiveReportAsync(reportCode, cancellationToken)
            ?? throw new NotFoundException($"Report '{reportCode}' was not found.");

        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 25;

        var entityType = _typeResolver.Resolve(report.EntityName);
        var method = typeof(ReportService)
            .GetMethod(nameof(ExecuteTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<ReportResultDto>)method.Invoke(this, [report, request, cancellationToken])!;
    }

    public async Task<byte[]> ExportExcelAsync(string reportCode, ReportQueryRequest request, CancellationToken cancellationToken = default)
    {
        var report = await LoadActiveReportAsync(reportCode, cancellationToken)
            ?? throw new NotFoundException($"Report '{reportCode}' was not found.");

        request.Page = 1;
        request.PageSize = _exportOptions.MaxExportRows;
        request.ExportAll = true;

        var result = await ExecuteAsync(reportCode, request, cancellationToken);
        return ReportExcelExporter.Export(result, MapExportLayout(report));
    }

    public async Task<byte[]> ExportPdfAsync(string reportCode, ReportQueryRequest request, CancellationToken cancellationToken = default)
    {
        var report = await LoadActiveReportAsync(reportCode, cancellationToken)
            ?? throw new NotFoundException($"Report '{reportCode}' was not found.");

        request.Page = 1;
        request.PageSize = _exportOptions.MaxExportRows;
        request.ExportAll = true;

        var result = await ExecuteAsync(reportCode, request, cancellationToken);
        return ReportPdfExporter.Export(result, MapExportLayout(report));
    }

    private async Task<ReportResultDto> ExecuteTypedAsync<T>(
        ForgeReport report,
        ReportQueryRequest request,
        CancellationToken cancellationToken) where T : class
    {
        var form = await _formCache.GetByEntityNameAsync(report.EntityName, cancellationToken);
        var displayColumns = GetDisplayColumns(report, form);

        return report.ReportType switch
        {
            ReportType.Grouped => await ExecuteGroupedAsync<T>(report, request, form, displayColumns, cancellationToken),
            ReportType.Summary => await ExecuteSummaryAsync<T>(report, request, form, displayColumns, cancellationToken),
            _ => await ExecuteTabularAsync<T>(report, request, form, displayColumns, cancellationToken)
        };
    }

    private async Task<ReportResultDto> ExecuteTabularAsync<T>(
        ForgeReport report,
        ReportQueryRequest request,
        ForgeForm? form,
        List<ReportColumnDefinitionDto> displayColumns,
        CancellationToken cancellationToken) where T : class
    {
        var plan = ReportQueryPlanner.Create<T>(report, request);
        var sourceProperties = plan.PropertyPaths;
        var gridRequest = BuildGridRequest(report, request, plan.SearchablePaths.ToList());

        IQueryable<T> query = _dbContext.Set<T>().AsNoTracking();
        query = ReportDynamicQuery.ApplyIncludes(query, plan.IncludePaths);
        query = ReportDynamicQuery.ApplySearch(query, gridRequest.SearchTerm, plan.SearchablePaths);
        query = ReportDynamicQuery.ApplyFilters(query, gridRequest.Filters);
        query = ReportDynamicQuery.ApplySort(query, gridRequest.SortColumn, gridRequest.SortDescending);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((gridRequest.Page - 1) * gridRequest.PageSize)
            .Take(gridRequest.PageSize)
            .ToListAsync(cancellationToken);

        var rows = await EnrichRowsAsync(items, report, form, displayColumns, cancellationToken);

        return new ReportResultDto
        {
            ReportType = ReportType.Tabular.ToString(),
            Columns = displayColumns,
            Rows = rows.Select(r => new ReportRowDto
            {
                RowType = ReportRowTypes.Detail,
                Values = r
            }).ToList(),
            TotalCount = total,
            DetailCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private async Task<ReportResultDto> ExecuteGroupedAsync<T>(
        ForgeReport report,
        ReportQueryRequest request,
        ForgeForm? form,
        List<ReportColumnDefinitionDto> displayColumns,
        CancellationToken cancellationToken) where T : class
    {
        var detailRows = await LoadAllDetailRowsAsync<T>(report, request, form, displayColumns, cancellationToken);
        var aggregateColumns = GetAggregateColumns(report);
        var summaries = report.Summaries.OrderBy(s => s.DisplayOrder).ToList();
        var built = ReportGroupingBuilder.BuildGrouped(report, detailRows, displayColumns, aggregateColumns, summaries);

        return BuildPagedResult(report, request, displayColumns, built);
    }

    private async Task<ReportResultDto> ExecuteSummaryAsync<T>(
        ForgeReport report,
        ReportQueryRequest request,
        ForgeForm? form,
        List<ReportColumnDefinitionDto> displayColumns,
        CancellationToken cancellationToken) where T : class
    {
        var detailRows = await LoadAllDetailRowsAsync<T>(report, request, form, displayColumns, cancellationToken);
        var aggregateColumns = GetAggregateColumns(report);
        var summaries = report.Summaries.OrderBy(s => s.DisplayOrder).ToList();
        var built = ReportGroupingBuilder.BuildSummary(report, detailRows, displayColumns, aggregateColumns, summaries);

        return BuildPagedResult(report, request, displayColumns, built);
    }

    private static ReportResultDto BuildPagedResult(
        ForgeReport report,
        ReportQueryRequest request,
        List<ReportColumnDefinitionDto> displayColumns,
        ReportBuildResult built)
    {
        var outputRows = request.ExportAll
            ? built.Rows
            : built.Rows
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

        return new ReportResultDto
        {
            ReportType = report.ReportType.ToString(),
            Columns = displayColumns,
            Rows = outputRows,
            GrandTotals = built.GrandTotals,
            TotalCount = built.Rows.Count,
            DetailCount = built.DetailCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private async Task<List<Dictionary<string, object?>>> LoadAllDetailRowsAsync<T>(
        ForgeReport report,
        ReportQueryRequest request,
        ForgeForm? form,
        List<ReportColumnDefinitionDto> displayColumns,
        CancellationToken cancellationToken) where T : class
    {
        var loadRequest = new ReportQueryRequest
        {
            Page = 1,
            PageSize = _exportOptions.MaxExportRows,
            SortColumn = request.SortColumn,
            SortDescending = request.SortDescending,
            SearchTerm = request.SearchTerm,
            FilterValues = request.FilterValues
        };
        var plan = ReportQueryPlanner.Create<T>(report, loadRequest);
        var gridRequest = BuildGridRequest(report, loadRequest, plan.SearchablePaths.ToList());

        IQueryable<T> query = _dbContext.Set<T>().AsNoTracking();
        query = ReportDynamicQuery.ApplyIncludes(query, plan.IncludePaths);
        query = ReportDynamicQuery.ApplySearch(query, gridRequest.SearchTerm, plan.SearchablePaths);
        query = ReportDynamicQuery.ApplyFilters(query, gridRequest.Filters);
        query = ReportDynamicQuery.ApplySort(query, gridRequest.SortColumn, gridRequest.SortDescending);

        var items = await query.Take(_exportOptions.MaxExportRows).ToListAsync(cancellationToken);

        return await EnrichRowsAsync(items, report, form, displayColumns, cancellationToken);
    }

    private async Task<List<Dictionary<string, object?>>> EnrichRowsAsync<T>(
        IEnumerable<T> items,
        ForgeReport report,
        ForgeForm? form,
        List<ReportColumnDefinitionDto> displayColumns,
        CancellationToken cancellationToken) where T : class
    {
        var sourceProperties = GetSourcePropertyColumns(report);
        var calculatedColumns = GetCalculatedColumnDefinitions(report, form);
        var enrichColumns = sourceProperties
            .Select(name =>
            {
                var forgeColumn = report.Columns.FirstOrDefault(c =>
                    string.Equals(c.PropertyName, name, StringComparison.OrdinalIgnoreCase));
                var columnDto = forgeColumn != null
                    ? MapColumn(forgeColumn, form)
                    : displayColumns.FirstOrDefault(c =>
                        string.Equals(c.PropertyName, name, StringComparison.OrdinalIgnoreCase))
                      ?? new ReportColumnDefinitionDto { PropertyName = name, Label = name };

                return new GridColumnDefinition
                {
                    PropertyName = columnDto.PropertyName,
                    Label = columnDto.Label,
                    ControlType = columnDto.ControlType,
                    LookupEntity = columnDto.LookupEntity,
                    DisplayFormat = columnDto.DisplayFormat
                };
            })
            .ToList();

        var rows = items.Select(i => ReportNavigationMapper.ToDictionary(i, sourceProperties)).ToList();
        await GridDisplayEnricher.EnrichAsync(rows, enrichColumns, _lookupService, formatTemporalColumns: true, cancellationToken);
        ReportFormulaEvaluator.ApplyCalculations(rows, calculatedColumns);
        return rows;
    }

    private GridQueryRequest BuildGridRequest(
        ForgeReport report,
        ReportQueryRequest request,
        List<string> searchable) => new()
    {
        Entity = report.EntityName,
        Page = request.Page,
        PageSize = request.PageSize,
        SortColumn = request.SortColumn,
        SortDescending = request.SortDescending,
        SearchTerm = request.SearchTerm,
        Filters = BuildFilterDictionary(report, request.FilterValues)
    };

    private async Task<ForgeReport?> LoadActiveReportAsync(string reportCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportCode))
            return null;

        var report = await _unitOfWork.Reports.GetByCodeAsync(reportCode.Trim().ToLowerInvariant(), cancellationToken);
        return report is { IsActive: true } ? report : null;
    }

    internal static ReportDefinitionDto MapDefinition(ForgeReport report, ForgeForm? form) => new()
    {
        Code = report.Code,
        Name = report.Name,
        EntityName = report.EntityName,
        ReportType = report.ReportType.ToString(),
        Description = report.Description,
        Columns = GetDisplayColumns(report, form),
        Filters = report.Filters
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new ReportFilterDefinitionDto
            {
                PropertyName = f.PropertyName,
                Label = f.Label,
                Operator = f.Operator.ToString(),
                ControlType = ReportFilterHelper.NormalizeControlType(f.ControlType),
                LookupEntity = f.LookupEntity,
                Options = f.Options,
                DefaultValue = f.DefaultValue,
                IsRequired = f.IsRequired
            }).ToList(),
        Groups = report.Groups
            .OrderBy(g => g.DisplayOrder)
            .Select(g => new ReportGroupDefinitionDto
            {
                PropertyName = g.PropertyName,
                Label = g.Label,
                SortDescending = g.SortDescending,
                ShowSubtotal = g.ShowSubtotal,
                ShowGroupHeader = g.ShowGroupHeader
            }).ToList(),
        Summaries = report.Summaries
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new ReportSummaryDefinitionDto
            {
                PropertyName = s.PropertyName,
                Label = s.Label,
                AggregateFunction = s.AggregateFunction.ToString()
            }).ToList(),
        ExportLayout = MapExportLayout(report)
    };

    internal static ReportExportLayoutDto MapExportLayout(ForgeReport report) => new()
    {
        Title = string.IsNullOrWhiteSpace(report.ExportTitle) ? report.Name : report.ExportTitle.Trim(),
        ShowTitleUnderline = report.ShowTitleUnderline,
        ShowSignatureBlock = report.ShowSignatureBlock,
        HeaderLeft = report.HeaderLeft,
        HeaderCenter = report.HeaderCenter,
        HeaderRight = report.HeaderRight,
        FooterLeft = report.FooterLeft,
        FooterCenter = report.FooterCenter,
        FooterRight = report.FooterRight,
        ShowPageNumbers = report.ShowPageNumbers,
        ShowGeneratedTimestamp = report.ShowGeneratedTimestamp,
        Signatures = report.Signatures
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new ReportSignatureLineDto
            {
                Id = s.Id,
                Label = s.Label,
                DisplayOrder = s.DisplayOrder
            }).ToList()
    };

    internal static List<ReportColumnDefinitionDto> GetDisplayColumns(ForgeReport report, ForgeForm? form) =>
        report.ReportType switch
        {
            ReportType.Summary => GetSummaryDisplayColumns(report, form),
            _ => GetRuntimeColumns(report, form)
        };

    internal static List<ReportColumnDefinitionDto> GetRuntimeColumns(ForgeReport report, ForgeForm? form) =>
        report.Columns
            .Where(c => c.IsVisible && c.ColumnRole is ReportColumnRole.Detail or ReportColumnRole.Aggregate or ReportColumnRole.Calculated)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapColumn(c, form))
            .ToList();

    internal static List<ReportColumnDefinitionDto> GetSummaryDisplayColumns(ForgeReport report, ForgeForm? form)
    {
        var groupProperties = report.Groups
            .OrderBy(g => g.DisplayOrder)
            .Select(g => g.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return report.Columns
            .Where(c => c.IsVisible
                && (c.ColumnRole == ReportColumnRole.Aggregate
                    || c.ColumnRole == ReportColumnRole.Calculated
                    || groupProperties.Contains(c.PropertyName)))
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapColumn(c, form))
            .ToList();
    }

    internal static List<ReportColumnDefinitionDto> GetCalculatedColumnDefinitions(ForgeReport report, ForgeForm? form) =>
        report.Columns
            .Where(c => c.IsVisible && c.ColumnRole == ReportColumnRole.Calculated)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => MapColumn(c, form))
            .ToList();

    internal static List<string> GetSourcePropertyColumns(ForgeReport report)
    {
        var calculatedNames = report.Columns
            .Where(c => c.ColumnRole == ReportColumnRole.Calculated)
            .Select(c => c.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var formulaDependencies = report.Columns
            .Where(c => c.ColumnRole == ReportColumnRole.Calculated && !string.IsNullOrWhiteSpace(c.Formula))
            .SelectMany(c => ReportFormulaEvaluator.ExtractDependencies(c.Formula))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return report.Columns
            .Where(c => c.ColumnRole != ReportColumnRole.Calculated)
            .Select(c => c.PropertyName)
            .Concat(formulaDependencies)
            .Where(name => !calculatedNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<ForgeReportColumn> GetAggregateColumns(ForgeReport report) =>
        report.Columns
            .Where(c => c.IsVisible && c.ColumnRole == ReportColumnRole.Aggregate)
            .OrderBy(c => c.DisplayOrder)
            .ToList();

    internal static ReportColumnDefinitionDto MapColumn(ForgeReportColumn column, ForgeForm? form)
    {
        var field = form?.Fields.FirstOrDefault(f =>
            string.Equals(f.PropertyName, column.PropertyName, StringComparison.OrdinalIgnoreCase));

        var lookupEntity = field?.LookupEntity;
        if (string.IsNullOrWhiteSpace(lookupEntity)
            && !column.PropertyName.Contains('.', StringComparison.Ordinal)
            && column.PropertyName.EndsWith("Id", StringComparison.Ordinal)
            && !string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase))
        {
            lookupEntity = column.PropertyName[..^2];
        }

        return new ReportColumnDefinitionDto
        {
            PropertyName = column.PropertyName,
            Label = string.IsNullOrWhiteSpace(column.Label) ? column.PropertyName : column.Label,
            IsSortable = true,
            IsVisible = column.IsVisible,
            ColumnRole = column.ColumnRole.ToString(),
            AggregateFunction = column.AggregateFunction.ToString(),
            ControlType = field?.ControlType,
            LookupEntity = lookupEntity,
            DisplayFormat = column.DisplayFormat,
            Formula = column.Formula
        };
    }

    internal static Dictionary<string, string> BuildFilterDictionary(
        ForgeReport report,
        Dictionary<string, string>? filterValues)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in report.Filters.OrderBy(f => f.DisplayOrder))
        {
            string? submitted = null;
            if (filterValues != null)
                filterValues.TryGetValue(filter.PropertyName, out submitted);

            var value = !string.IsNullOrWhiteSpace(submitted) ? submitted.Trim() : filter.DefaultValue?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var controlType = ReportFilterHelper.NormalizeControlType(filter.ControlType);
            if (string.Equals(controlType, ReportFilterControlType.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                ReportFilterHelper.ApplyDateRangeValue(filter.PropertyName, value, result);
                continue;
            }

            result[ToFilterKey(filter.PropertyName, filter.Operator)] = value;
        }

        return result;
    }

    internal static string ToFilterKey(string propertyName, FilterOperator op) => op switch
    {
        FilterOperator.NotEquals => $"{propertyName}__ne",
        FilterOperator.Contains => $"{propertyName}__contains",
        FilterOperator.StartsWith => $"{propertyName}__startswith",
        FilterOperator.GreaterThan => $"{propertyName}__gt",
        FilterOperator.LessThan => $"{propertyName}__lt",
        FilterOperator.GreaterOrEqual => $"{propertyName}__gte",
        FilterOperator.LessOrEqual => $"{propertyName}__lte",
        FilterOperator.Between => $"{propertyName}__between",
        _ => propertyName
    };

}
