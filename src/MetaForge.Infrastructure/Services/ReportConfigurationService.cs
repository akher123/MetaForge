namespace MetaForge.Infrastructure.Services;

using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Reports;

/// <summary>
/// Creates and manages dynamic report metadata.
/// </summary>
public class ReportConfigurationService : IReportConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEntityMetadataDiscoveryService _discoveryService;
    private readonly ISecurityManagementService _securityManagementService;
    private readonly IEntityTypeResolver _typeResolver;

    public ReportConfigurationService(
        IUnitOfWork unitOfWork,
        IEntityMetadataDiscoveryService discoveryService,
        ISecurityManagementService securityManagementService,
        IEntityTypeResolver typeResolver)
    {
        _unitOfWork = unitOfWork;
        _discoveryService = discoveryService;
        _securityManagementService = securityManagementService;
        _typeResolver = typeResolver;
    }

    public async Task<IReadOnlyList<ReportConfigListItemDto>> GetAllReportsAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports.GetAllAsync(cancellationToken);
        return reports
            .OrderBy(r => r.GroupName)
            .ThenBy(r => r.DisplayOrder)
            .Select(r => new ReportConfigListItemDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                EntityName = r.EntityName,
                GroupName = r.GroupName ?? "Reports",
                ReportType = r.ReportType.ToString(),
                IsActive = r.IsActive,
                ColumnCount = r.Columns?.Count ?? 0,
                FilterCount = r.Filters?.Count ?? 0
            }).ToList();
    }

    public async Task<ReportConfigDto?> GetReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.GetByIdAsync(id, cancellationToken);
        return report == null ? null : MapToDto(report);
    }

    public Task<IReadOnlyList<DiscoveredEntityOptionDto>> GetDiscoveredEntitiesAsync(CancellationToken cancellationToken = default)
    {
        var discovered = _discoveryService.DiscoverAll();
        var options = discovered.Select(d => new DiscoveredEntityOptionDto
        {
            EntityName = d.EntityName,
            TableName = d.TableName,
            IsConfigured = false,
            Metadata = d
        }).ToList();

        return Task.FromResult<IReadOnlyList<DiscoveredEntityOptionDto>>(options);
    }

    public Task<ReportConfigDto> BuildDraftAsync(string entityName, string groupName, CancellationToken cancellationToken = default)
    {
        var metadata = _discoveryService.Discover(entityName)
            ?? throw new NotFoundException($"Entity '{entityName}' was not found.");

        var properties = metadata.Properties
            .Where(p => p.Name != "Id" || p.IsKey)
            .ToList();

        var draft = new ReportConfigDto
        {
            Code = $"{entityName}-report".ToLowerInvariant(),
            Name = $"{SplitPascalCase(entityName)} Report",
            EntityName = entityName,
            GroupName = NormalizeGroupName(groupName),
            ReportType = ReportType.Tabular.ToString(),
            DisplayOrder = 0,
            IsActive = true,
            Description = $"Tabular report for {SplitPascalCase(entityName)}.",
            Columns = properties
                .Where(p => !p.IsForeignKey || p.Name.EndsWith("Id", StringComparison.Ordinal))
                .Take(8)
                .Select((p, i) => new ReportColumnConfigDto
                {
                    PropertyName = p.Name,
                    Label = SplitPascalCase(p.Name),
                    DisplayOrder = i,
                    IsVisible = p.Name != "Id",
                    ColumnRole = ReportColumnRole.Detail.ToString(),
                    AggregateFunction = ReportAggregateFunction.None.ToString()
                }).ToList(),
            Filters = properties
                .Where(p => p.ClrType.Contains("String", StringComparison.Ordinal)
                    || p.ClrType.Contains("DateTime", StringComparison.Ordinal)
                    || p.ClrType.Contains("Int", StringComparison.Ordinal)
                    || p.ClrType.Contains("Decimal", StringComparison.Ordinal))
                .Take(3)
                .Select((p, i) =>
                {
                    var inferred = ReportFilterHelper.InferForProperty(p.Name, p.ClrType, p.IsForeignKey);
                    return new ReportFilterConfigDto
                    {
                        PropertyName = p.Name,
                        Label = SplitPascalCase(p.Name),
                        Operator = inferred.Operator.ToString(),
                        ControlType = inferred.ControlType,
                        LookupEntity = inferred.LookupEntity,
                        Options = inferred.Options,
                        DisplayOrder = i
                    };
                }).ToList()
        };

        return Task.FromResult(draft);
    }

    public Task<IReadOnlyList<ReportPropertyOptionDto>> GetEntityPropertyPathsAsync(
        string entityName,
        CancellationToken cancellationToken = default)
    {
        var entityType = _typeResolver.Resolve(entityName);
        var paths = ReportPropertyPathResolver.DiscoverPaths(entityType);
        IReadOnlyList<ReportPropertyOptionDto> result = paths
            .Select(p => new ReportPropertyOptionDto
            {
                Path = p.Path,
                Label = p.Label,
                ClrType = p.ClrType,
                IsForeignKey = p.IsForeignKey
            })
            .ToList();

        return Task.FromResult(result);
    }

    public async Task<int> SaveReportAsync(ReportConfigDto config, CancellationToken cancellationToken = default)
    {
        Validate(config);

        if (await _unitOfWork.Reports.ExistsByCodeAsync(config.Code, config.Id > 0 ? config.Id : null, cancellationToken))
            throw new BusinessException($"Report code '{config.Code}' already exists.");

        ForgeReport report;

        if (config.Id > 0)
        {
            report = await _unitOfWork.Reports.GetByIdTrackedAsync(config.Id, cancellationToken)
                ?? throw new NotFoundException($"Report {config.Id} was not found.");

            report.Columns.Clear();
            report.Filters.Clear();
            report.Groups.Clear();
            report.Summaries.Clear();
            report.Signatures.Clear();
        }
        else
        {
            report = new ForgeReport();
            await _unitOfWork.Reports.AddAsync(report, cancellationToken);
        }

        report.Code = config.Code.Trim().ToLowerInvariant();
        report.Name = config.Name.Trim();
        report.EntityName = config.EntityName.Trim();
        report.GroupName = NormalizeGroupName(config.GroupName);
        report.ReportType = ParseReportType(config.ReportType);
        report.DisplayOrder = config.DisplayOrder;
        report.IsActive = config.IsActive;
        report.Description = string.IsNullOrWhiteSpace(config.Description) ? null : config.Description.Trim();
        report.ExportTitle = string.IsNullOrWhiteSpace(config.ExportTitle) ? null : config.ExportTitle.Trim();
        report.ShowTitleUnderline = config.ShowTitleUnderline;
        report.ShowSignatureBlock = config.ShowSignatureBlock;
        report.HeaderLeft = NormalizeOptionalText(config.HeaderLeft);
        report.HeaderCenter = NormalizeOptionalText(config.HeaderCenter);
        report.HeaderRight = NormalizeOptionalText(config.HeaderRight);
        report.FooterLeft = NormalizeOptionalText(config.FooterLeft);
        report.FooterCenter = NormalizeOptionalText(config.FooterCenter);
        report.FooterRight = NormalizeOptionalText(config.FooterRight);
        report.ShowPageNumbers = config.ShowPageNumbers;
        report.ShowGeneratedTimestamp = config.ShowGeneratedTimestamp;

        foreach (var column in config.Columns.Select((c, i) => new ForgeReportColumn
        {
            PropertyName = c.PropertyName.Trim(),
            Label = c.Label.Trim(),
            DisplayOrder = c.DisplayOrder >= 0 ? c.DisplayOrder : i,
            IsVisible = c.IsVisible,
            ColumnRole = ParseColumnRole(c.ColumnRole),
            AggregateFunction = ParseAggregateFunction(c.AggregateFunction),
            DisplayFormat = string.IsNullOrWhiteSpace(c.DisplayFormat) ? null : c.DisplayFormat.Trim(),
            Formula = string.IsNullOrWhiteSpace(c.Formula) ? null : c.Formula.Trim()
        }))
        {
            if (string.IsNullOrWhiteSpace(column.PropertyName))
                continue;

            report.Columns.Add(column);
        }

        foreach (var filter in config.Filters.Select((f, i) =>
        {
            var controlType = ReportFilterHelper.NormalizeControlType(f.ControlType);
            var op = ParseFilterOperator(f.Operator);
            op = ReportFilterHelper.NormalizeOperator(controlType, op);

            return new ForgeReportFilter
            {
                PropertyName = f.PropertyName.Trim(),
                Label = f.Label.Trim(),
                Operator = op,
                ControlType = controlType,
                LookupEntity = string.IsNullOrWhiteSpace(f.LookupEntity) ? null : f.LookupEntity.Trim(),
                Options = string.IsNullOrWhiteSpace(f.Options) ? null : f.Options.Trim(),
                DefaultValue = string.IsNullOrWhiteSpace(f.DefaultValue) ? null : f.DefaultValue.Trim(),
                IsRequired = f.IsRequired,
                DisplayOrder = f.DisplayOrder >= 0 ? f.DisplayOrder : i
            };
        }))
        {
            if (string.IsNullOrWhiteSpace(filter.PropertyName))
                continue;

            report.Filters.Add(filter);
        }

        foreach (var group in config.Groups.Select((g, i) => new ForgeReportGroup
        {
            PropertyName = g.PropertyName.Trim(),
            Label = g.Label.Trim(),
            DisplayOrder = g.DisplayOrder >= 0 ? g.DisplayOrder : i,
            SortDescending = g.SortDescending,
            ShowSubtotal = g.ShowSubtotal,
            ShowGroupHeader = g.ShowGroupHeader
        }))
        {
            if (string.IsNullOrWhiteSpace(group.PropertyName))
                continue;

            report.Groups.Add(group);
        }

        foreach (var summary in config.Summaries.Select((s, i) => new ForgeReportSummary
        {
            PropertyName = s.PropertyName.Trim(),
            Label = s.Label.Trim(),
            AggregateFunction = ParseAggregateFunction(s.AggregateFunction),
            DisplayOrder = s.DisplayOrder >= 0 ? s.DisplayOrder : i
        }))
        {
            if (string.IsNullOrWhiteSpace(summary.PropertyName))
                continue;

            report.Summaries.Add(summary);
        }

        foreach (var signature in config.Signatures.Select((s, i) => new ForgeReportSignature
        {
            Label = s.Label.Trim(),
            DisplayOrder = s.DisplayOrder >= 0 ? s.DisplayOrder : i
        }))
        {
            if (string.IsNullOrWhiteSpace(signature.Label))
                continue;

            report.Signatures.Add(signature);
        }

        EnsureReportShape(report);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _securityManagementService.SyncReportPermissionsAsync(cancellationToken);

        return report.Id;
    }

    public async Task DeleteReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var report = await _unitOfWork.Reports.GetByIdTrackedAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Report {id} was not found.");

        _unitOfWork.Reports.Remove(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _securityManagementService.SyncReportPermissionsAsync(cancellationToken);
    }

    private void Validate(ReportConfigDto config)
    {
        if (string.IsNullOrWhiteSpace(config.Code))
            throw new BusinessException("Report code is required.");

        if (string.IsNullOrWhiteSpace(config.Name))
            throw new BusinessException("Report name is required.");

        if (string.IsNullOrWhiteSpace(config.EntityName))
            throw new BusinessException("Entity name is required.");

        if (config.Columns.Count == 0)
            throw new BusinessException("Add at least one column to the report.");

        var entityType = _typeResolver.Resolve(config.EntityName.Trim());

        foreach (var column in config.Columns)
        {
            var role = ParseColumnRole(column.ColumnRole);
            if (role == ReportColumnRole.Calculated && string.IsNullOrWhiteSpace(column.Formula))
                throw new BusinessException($"Calculated column '{column.PropertyName}' requires a formula.");

            if (role != ReportColumnRole.Calculated && !string.IsNullOrWhiteSpace(column.Formula))
                throw new BusinessException($"Formula is only allowed on calculated columns ('{column.PropertyName}').");

            if (role != ReportColumnRole.Calculated
                && !string.IsNullOrWhiteSpace(column.PropertyName)
                && !ReportPropertyPathResolver.IsValidPath(entityType, column.PropertyName))
            {
                throw new BusinessException($"Unknown property path '{column.PropertyName}' on entity '{config.EntityName}'.");
            }
        }

        ValidatePathFields(entityType, config.EntityName, config.Filters.Select(f => f.PropertyName), "filter");
        ValidatePathFields(entityType, config.EntityName, config.Groups.Select(g => g.PropertyName), "group");
        ValidatePathFields(entityType, config.EntityName, config.Summaries.Select(s => s.PropertyName), "summary");

        foreach (var filter in config.Filters)
        {
            var controlType = ReportFilterHelper.NormalizeControlType(filter.ControlType);
            if (string.Equals(controlType, ReportFilterControlType.Dropdown, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(filter.LookupEntity)
                && string.IsNullOrWhiteSpace(filter.Options))
            {
                throw new BusinessException($"Dropdown filter '{filter.PropertyName}' requires Lookup Entity or Options.");
            }

            if (string.Equals(controlType, ReportFilterControlType.Autocomplete, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(filter.LookupEntity))
            {
                throw new BusinessException($"Autocomplete filter '{filter.PropertyName}' requires Lookup Entity.");
            }
        }
    }

    private static void ValidatePathFields(
        Type entityType,
        string entityName,
        IEnumerable<string> paths,
        string fieldKind)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (!ReportPropertyPathResolver.IsValidPath(entityType, path))
                throw new BusinessException($"Unknown {fieldKind} property path '{path}' on entity '{entityName}'.");
        }
    }

    private static void EnsureReportShape(ForgeReport report)
    {
        if (report.ReportType == ReportType.Grouped && report.Groups.Count == 0)
            throw new BusinessException("Grouped reports require at least one group field.");

        if (report.ReportType == ReportType.Summary && report.Summaries.Count == 0)
            throw new BusinessException("Summary reports require at least one summary/total row.");

        if (report.ShowSignatureBlock && report.Signatures.Count == 0)
            throw new BusinessException("Add at least one signature line or disable the signature block.");
    }

    private static ReportConfigDto MapToDto(ForgeReport report) => new()
    {
        Id = report.Id,
        Code = report.Code,
        Name = report.Name,
        EntityName = report.EntityName,
        GroupName = report.GroupName ?? "Reports",
        ReportType = report.ReportType.ToString(),
        DisplayOrder = report.DisplayOrder,
        IsActive = report.IsActive,
        Description = report.Description,
        ExportTitle = report.ExportTitle,
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
        Columns = report.Columns
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new ReportColumnConfigDto
            {
                Id = c.Id,
                PropertyName = c.PropertyName,
                Label = c.Label,
                DisplayOrder = c.DisplayOrder,
                IsVisible = c.IsVisible,
                ColumnRole = c.ColumnRole.ToString(),
                AggregateFunction = c.AggregateFunction.ToString(),
                DisplayFormat = c.DisplayFormat,
                Formula = c.Formula
            }).ToList(),
        Filters = report.Filters
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new ReportFilterConfigDto
            {
                Id = f.Id,
                PropertyName = f.PropertyName,
                Label = f.Label,
                Operator = f.Operator.ToString(),
                ControlType = f.ControlType,
                LookupEntity = f.LookupEntity,
                Options = f.Options,
                DefaultValue = f.DefaultValue,
                IsRequired = f.IsRequired,
                DisplayOrder = f.DisplayOrder
            }).ToList(),
        Groups = report.Groups
            .OrderBy(g => g.DisplayOrder)
            .Select(g => new ReportGroupConfigDto
            {
                Id = g.Id,
                PropertyName = g.PropertyName,
                Label = g.Label,
                DisplayOrder = g.DisplayOrder,
                SortDescending = g.SortDescending,
                ShowSubtotal = g.ShowSubtotal,
                ShowGroupHeader = g.ShowGroupHeader
            }).ToList(),
        Summaries = report.Summaries
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new ReportSummaryConfigDto
            {
                Id = s.Id,
                PropertyName = s.PropertyName,
                Label = s.Label,
                AggregateFunction = s.AggregateFunction.ToString(),
                DisplayOrder = s.DisplayOrder
            }).ToList(),
        Signatures = report.Signatures
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new ReportSignatureLineDto
            {
                Id = s.Id,
                Label = s.Label,
                DisplayOrder = s.DisplayOrder
            }).ToList()
    };

    private static ReportType ParseReportType(string value) =>
        Enum.TryParse<ReportType>(value, true, out var parsed) ? parsed : ReportType.Tabular;

    private static ReportColumnRole ParseColumnRole(string value) =>
        Enum.TryParse<ReportColumnRole>(value, true, out var parsed) ? parsed : ReportColumnRole.Detail;

    private static ReportAggregateFunction ParseAggregateFunction(string value) =>
        Enum.TryParse<ReportAggregateFunction>(value, true, out var parsed) ? parsed : ReportAggregateFunction.None;

    private static FilterOperator ParseFilterOperator(string value) =>
        Enum.TryParse<FilterOperator>(value, true, out var parsed) ? parsed : FilterOperator.Equals;

    private static FilterOperator InferDefaultOperator(string clrType)
    {
        if (clrType.Contains("String", StringComparison.Ordinal))
            return FilterOperator.Contains;

        if (clrType.Contains("DateTime", StringComparison.Ordinal)
            || clrType.Contains("Int", StringComparison.Ordinal)
            || clrType.Contains("Decimal", StringComparison.Ordinal)
            || clrType.Contains("Double", StringComparison.Ordinal))
            return FilterOperator.GreaterOrEqual;

        return FilterOperator.Equals;
    }

    private static string NormalizeGroupName(string groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? "Reports" : groupName.Trim();

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return string.Concat(value.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]) ? " " + c : c.ToString()));
    }
}
