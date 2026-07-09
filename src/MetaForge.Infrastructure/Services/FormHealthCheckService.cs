using MetaForge.Application.DTOs;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Aggregates metadata health checks across configured forms, permissions, lookups, and relations.
/// </summary>
public class FormHealthCheckService : IFormHealthCheckService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityMetadataDiscoveryService _discoveryService;
    private readonly IFormConfigurationService _formConfigurationService;

    public FormHealthCheckService(
        MetaForgeDbContext dbContext,
        IEntityMetadataDiscoveryService discoveryService,
        IFormConfigurationService formConfigurationService)
    {
        _dbContext = dbContext;
        _discoveryService = discoveryService;
        _formConfigurationService = formConfigurationService;
    }

    public async Task<FormHealthReportDto> GetReportAsync(CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(cancellationToken);
        var report = new FormHealthReportDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalForms = context.Forms.Count
        };

        AppendGlobalIssues(report, context);

        foreach (var form in context.Forms)
        {
            var item = await BuildFormHealthItemAsync(form, context, cancellationToken);
            report.Items.Add(item);

            switch (item.Status)
            {
                case FormHealthStatus.Error:
                    report.ErrorCount++;
                    break;
                case FormHealthStatus.Warning:
                    report.WarningCount++;
                    break;
                default:
                    report.HealthyCount++;
                    break;
            }
        }

        report.FormsNeedingAttention = report.Items.Count(i => i.Status != FormHealthStatus.Healthy);
        return report;
    }

    public async Task<FormHealthItemDto?> GetFormHealthAsync(int formId, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(cancellationToken);
        var form = context.Forms.FirstOrDefault(f => f.Id == formId);
        return form == null ? null : await BuildFormHealthItemAsync(form, context, cancellationToken);
    }

    private async Task<HealthCheckContext> BuildContextAsync(CancellationToken cancellationToken)
    {
        var forms = await _dbContext.ForgeForms
            .Include(m => m.Fields)
            .Include(m => m.Relations)
            .Include(m => m.GridColumns)
            .Include(m => m.GridActions)
            .AsNoTracking()
            .OrderBy(m => m.GroupName)
            .ThenBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);

        var permissionCodes = (await _dbContext.Permissions
            .AsNoTracking()
            .Select(p => p.Code)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lookupEntities = (await _dbContext.LookupConfigurations
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.EntityName)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var menuFormIds = (await _dbContext.ForgeMenus
            .AsNoTracking()
            .Where(m => m.FormId != null && m.IsActive)
            .Select(m => m.FormId!.Value)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var discovered = _discoveryService.DiscoverAll();
        var discoveredByEntity = discovered.ToDictionary(d => d.EntityName, StringComparer.OrdinalIgnoreCase);
        var configuredEntities = forms.Select(f => f.EntityName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new HealthCheckContext(
            forms,
            permissionCodes,
            lookupEntities,
            menuFormIds,
            discovered,
            discoveredByEntity,
            configuredEntities);
    }

    private static void AppendGlobalIssues(FormHealthReportDto report, HealthCheckContext context)
    {
        foreach (var entity in context.Discovered.Where(d => !context.ConfiguredEntities.Contains(d.EntityName)))
        {
            report.GlobalIssues.Add(new FormHealthGlobalIssueDto
            {
                Category = FormHealthIssueCategories.Discovery,
                Severity = FormHealthSeverity.Info,
                Message = $"Entity '{entity.EntityName}' is discoverable but has no configured form.",
                ActionUrl = $"/FormBuilder/Create?entity={Uri.EscapeDataString(entity.EntityName)}"
            });
        }
    }

    private async Task<FormHealthItemDto> BuildFormHealthItemAsync(
        ForgeForm form,
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<FormHealthIssueDto>();
        var editUrl = $"/FormBuilder/Edit/{form.Id}";
        var moduleUrl = $"/Modules/{form.Code}";
        var formDto = MapToDto(form);
        var isDetailForm = form.FormType == FormType.Detail;

        if (!form.IsActive)
        {
            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Configuration,
                Severity = FormHealthSeverity.Warning,
                Message = "Form is inactive.",
                ActionUrl = editUrl
            });
        }

        if (form.Fields.Count == 0)
        {
            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Configuration,
                Severity = FormHealthSeverity.Error,
                Message = "Form has no fields configured.",
                ActionUrl = editUrl
            });
        }

        if (!isDetailForm && form.GridColumns.Count == 0)
        {
            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Configuration,
                Severity = FormHealthSeverity.Error,
                Message = "List grid has no columns configured.",
                ActionUrl = editUrl
            });
        }

        if (!context.DiscoveredByEntity.TryGetValue(form.EntityName, out var metadata))
        {
            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Discovery,
                Severity = FormHealthSeverity.Error,
                Message = $"Entity '{form.EntityName}' is no longer discoverable in EF Core.",
                ActionUrl = editUrl
            });
        }
        else
        {
            await AppendSchemaIssuesAsync(issues, formDto, metadata, cancellationToken);
        }

        AppendPermissionIssues(issues, form, context.PermissionCodes);
        AppendLookupIssues(issues, form, context.LookupEntities, context.DiscoveredByEntity, editUrl);
        AppendRelationIssues(issues, form, context, editUrl);
        AppendMenuIssues(issues, form, context.MenuFormIds);
        AppendGridActionIssues(issues, form, editUrl);

        if (IsMasterDetailScreen(form))
            await AppendChildSchemaIssuesAsync(issues, form, context, editUrl, cancellationToken);

        return new FormHealthItemDto
        {
            FormId = form.Id,
            Code = form.Code,
            Name = form.Name,
            EntityName = form.EntityName,
            GroupName = form.GroupName ?? "General",
            FormType = form.FormType.ToString(),
            IsActive = form.IsActive,
            Status = ResolveStatus(issues),
            IssueCount = issues.Count,
            EditUrl = editUrl,
            ModuleUrl = moduleUrl,
            Issues = issues
        };
    }

    private async Task AppendChildSchemaIssuesAsync(
        List<FormHealthIssueDto> issues,
        ForgeForm masterForm,
        HealthCheckContext context,
        string editUrl,
        CancellationToken cancellationToken)
    {
        foreach (var relation in masterForm.Relations
                     .Where(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(r => r.DisplayOrder))
        {
            var childForm = context.Forms.FirstOrDefault(f =>
                f.EntityName.Equals(relation.ChildEntity, StringComparison.OrdinalIgnoreCase));

            if (childForm == null)
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Relation,
                    Severity = FormHealthSeverity.Error,
                    Message = $"Missing detail form for child entity '{relation.ChildEntity}'.",
                    ActionUrl = editUrl
                });
                continue;
            }

            if (!context.DiscoveredByEntity.TryGetValue(relation.ChildEntity, out var childMetadata))
                continue;

            var childDto = MapToDto(childForm);
            var draft = await _formConfigurationService.BuildDraftAsync(
                relation.ChildEntity,
                masterForm.GroupName ?? "Master Data",
                cancellationToken);
            draft.FormType = FormType.Detail.ToString();
            ApplyDetailFormDefaults(childDto, relation.ForeignKey);
            ApplyDetailFormDefaults(draft, relation.ForeignKey);

            var preview = FormSchemaSyncPlanner.BuildPreview(childDto, childMetadata, draft);
            if (!preview.HasChanges)
                continue;

            var label = string.IsNullOrWhiteSpace(relation.TabLabel)
                ? relation.ChildEntity
                : relation.TabLabel;
            var severity = preview.Changes.Any(c =>
                c.ChangeType == FormSchemaSyncChangeTypes.Remove)
                ? FormHealthSeverity.Error
                : FormHealthSeverity.Warning;

            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Schema,
                Severity = severity,
                Message = $"Detail form '{label}' is out of sync with entity ({preview.Changes.Count} change(s)).",
                ActionUrl = editUrl
            });
        }
    }

    private async Task AppendSchemaIssuesAsync(
        List<FormHealthIssueDto> issues,
        FormConfigDto form,
        EntityMetadataDto metadata,
        CancellationToken cancellationToken)
    {
        var draft = await _formConfigurationService.BuildDraftAsync(
            form.EntityName,
            form.GroupName,
            cancellationToken);
        draft.FormType = form.FormType;

        var preview = FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);
        if (!preview.HasChanges)
            return;

        var severity = preview.Changes.Any(c => c.ChangeType == FormSchemaSyncChangeTypes.Remove)
            ? FormHealthSeverity.Error
            : FormHealthSeverity.Warning;

        issues.Add(new FormHealthIssueDto
        {
            Category = FormHealthIssueCategories.Schema,
            Severity = severity,
            Message = $"Form schema is out of sync with entity ({preview.Changes.Count} change(s)).",
            ActionUrl = $"/FormBuilder/Edit/{form.Id}"
        });
    }

    private static void AppendPermissionIssues(
        List<FormHealthIssueDto> issues,
        ForgeForm form,
        HashSet<string> permissionCodes)
    {
        if (!form.IsActive)
            return;

        var missing = PermissionAction.All
            .Where(action => !permissionCodes.Contains($"{form.Code}.{action}"))
            .ToList();

        if (missing.Count == 0)
            return;

        issues.Add(new FormHealthIssueDto
        {
            Category = FormHealthIssueCategories.Permission,
            Severity = FormHealthSeverity.Warning,
            Message = $"Missing permissions: {string.Join(", ", missing)}.",
            ActionUrl = "/Security/Permissions"
        });
    }

    private static void AppendLookupIssues(
        List<FormHealthIssueDto> issues,
        ForgeForm form,
        HashSet<string> lookupEntities,
        Dictionary<string, EntityMetadataDto> discoveredByEntity,
        string editUrl)
    {
        foreach (var field in form.Fields.Where(f => ControlType.IsLookupOrMultiSelect(f.ControlType)))
        {
            if (string.IsNullOrWhiteSpace(field.LookupEntity))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Lookup,
                    Severity = FormHealthSeverity.Error,
                    Message = $"Field '{field.PropertyName}' is a lookup but has no lookup entity.",
                    ActionUrl = editUrl
                });
                continue;
            }

            if (!discoveredByEntity.ContainsKey(field.LookupEntity))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Lookup,
                    Severity = FormHealthSeverity.Error,
                    Message = $"Lookup entity '{field.LookupEntity}' for field '{field.PropertyName}' is not discoverable.",
                    ActionUrl = editUrl
                });
            }

            if (!lookupEntities.Contains(field.LookupEntity))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Lookup,
                    Severity = FormHealthSeverity.Warning,
                    Message = $"Lookup configuration missing for entity '{field.LookupEntity}' (field '{field.PropertyName}').",
                    ActionUrl = editUrl
                });
            }
        }
    }

    private static void AppendRelationIssues(
        List<FormHealthIssueDto> issues,
        ForgeForm form,
        HealthCheckContext context,
        string editUrl)
    {
        if (form.FormType != FormType.Detail)
            return;

        var hasParent = context.Forms.Any(parent =>
            parent.Relations.Any(r =>
                r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase)
                && r.ChildEntity.Equals(form.EntityName, StringComparison.OrdinalIgnoreCase)));

        if (!hasParent)
        {
            issues.Add(new FormHealthIssueDto
            {
                Category = FormHealthIssueCategories.Relation,
                Severity = FormHealthSeverity.Warning,
                Message = $"Detail form '{form.EntityName}' is not referenced by any master relation.",
                ActionUrl = editUrl
            });
        }
    }

    private static void AppendMenuIssues(
        List<FormHealthIssueDto> issues,
        ForgeForm form,
        HashSet<int> menuFormIds)
    {
        if (!form.IsActive || form.FormType == FormType.Detail)
            return;

        if (menuFormIds.Contains(form.Id))
            return;

        issues.Add(new FormHealthIssueDto
        {
            Category = FormHealthIssueCategories.Menu,
            Severity = FormHealthSeverity.Warning,
            Message = "Active form is not linked to a sidebar menu item.",
            ActionUrl = "/Menu"
        });
    }

    private static void AppendGridActionIssues(
        List<FormHealthIssueDto> issues,
        ForgeForm form,
        string editUrl)
    {
        var fieldNames = form.Fields.Select(f => f.PropertyName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columnNames = form.GridColumns.Select(c => c.PropertyName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in form.GridActions.Where(a => a.IsActive))
        {
            if (string.IsNullOrWhiteSpace(action.Code) || string.IsNullOrWhiteSpace(action.Label))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Configuration,
                    Severity = FormHealthSeverity.Error,
                    Message = "Grid action is missing code or label.",
                    ActionUrl = editUrl
                });
            }

            if (string.IsNullOrWhiteSpace(action.HandlerTarget)
                && !string.Equals(action.HandlerType, GridActionHandlerType.Script, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Configuration,
                    Severity = FormHealthSeverity.Warning,
                    Message = $"Grid action '{action.Code}' has no handler target.",
                    ActionUrl = editUrl
                });
            }

            if (!string.IsNullOrWhiteSpace(action.PermissionAction)
                && !PermissionAction.All.Contains(action.PermissionAction, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Configuration,
                    Severity = FormHealthSeverity.Warning,
                    Message = $"Grid action '{action.Code}' references unknown permission action '{action.PermissionAction}'.",
                    ActionUrl = editUrl
                });
            }

            foreach (var name in ExtractPropertyReferences(action.RequestBody))
            {
                if (fieldNames.Contains(name) || columnNames.Contains(name))
                    continue;

                issues.Add(new FormHealthIssueDto
                {
                    Category = FormHealthIssueCategories.Configuration,
                    Severity = FormHealthSeverity.Warning,
                    Message = $"Grid action '{action.Code}' references unknown field '{name}'.",
                    ActionUrl = editUrl
                });
            }
        }
    }

    private static IEnumerable<string> ExtractPropertyReferences(string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            yield break;

        foreach (var token in requestBody.Split(['{', '}', '"', ':', ',', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length == 0 || char.IsDigit(token[0]))
                continue;
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase)
                || token.Equals("false", StringComparison.OrdinalIgnoreCase)
                || token.Equals("null", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return token;
        }
    }

    private static bool IsMasterDetailScreen(ForgeForm form) =>
        form.FormType == FormType.MasterDetail || form.FormType == FormType.MasterDetailTabular;

    private static void ApplyDetailFormDefaults(FormConfigDto detail, string foreignKey)
    {
        if (string.IsNullOrWhiteSpace(foreignKey))
            return;

        foreach (var field in detail.Fields.Where(f =>
                     f.PropertyName.Equals(foreignKey, StringComparison.OrdinalIgnoreCase)))
        {
            field.IsVisible = false;
            field.ControlType = ControlType.Hidden;
        }
    }

    private static string ResolveStatus(IReadOnlyList<FormHealthIssueDto> issues)
    {
        if (issues.Any(i => i.Severity == FormHealthSeverity.Error))
            return FormHealthStatus.Error;
        if (issues.Any(i => i.Severity == FormHealthSeverity.Warning))
            return FormHealthStatus.Warning;
        return FormHealthStatus.Healthy;
    }

    private static FormConfigDto MapToDto(ForgeForm module) => new()
    {
        Id = module.Id,
        Code = module.Code,
        Name = module.Name,
        EntityName = module.EntityName,
        TableName = module.TableName,
        GroupName = module.GroupName ?? "Master Data",
        FormType = module.FormType.ToString(),
        DisplayOrder = module.DisplayOrder,
        IsActive = module.IsActive,
        Fields = module.Fields.OrderBy(f => f.DisplayOrder).Select(f => new FormFieldConfigDto
        {
            Id = f.Id,
            PropertyName = f.PropertyName,
            Label = f.Label,
            ControlType = f.ControlType,
            IsRequired = f.IsRequired,
            IsVisible = f.IsVisible,
            IsReadOnly = f.IsReadOnly,
            DisplayOrder = f.DisplayOrder,
            ValidationRule = f.ValidationRule,
            ConditionalRule = f.ConditionalRule,
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
            MappingEntity = f.MappingEntity,
            MappingParentKey = f.MappingParentKey,
            MappingRelatedKey = f.MappingRelatedKey,
            SectionName = f.SectionName
        }).ToList(),
        GridColumns = module.GridColumns.OrderBy(c => c.DisplayOrder).Select(c => new FormGridColumnConfigDto
        {
            Id = c.Id,
            PropertyName = c.PropertyName,
            Label = c.Label,
            DisplayOrder = c.DisplayOrder,
            IsSortable = c.IsSortable,
            IsSearchable = c.IsSearchable,
            IsVisible = c.IsVisible,
            DisplayFormat = c.DisplayFormat
        }).ToList(),
        GridActions = module.GridActions.OrderBy(a => a.DisplayOrder).Select(a => new FormGridActionConfigDto
        {
            Id = a.Id,
            Code = a.Code,
            Label = a.Label,
            Icon = a.Icon,
            Placement = a.Placement,
            HandlerType = a.HandlerType,
            HandlerTarget = a.HandlerTarget,
            HttpMethod = a.HttpMethod,
            RequestBody = a.RequestBody,
            PermissionAction = a.PermissionAction,
            ConfirmMessage = a.ConfirmMessage,
            ButtonStyle = a.ButtonStyle,
            DisplayOrder = a.DisplayOrder,
            IsActive = a.IsActive
        }).ToList(),
        Relations = module.Relations.Select(r => new FormRelationConfigDto
        {
            Id = r.Id,
            RelationType = r.RelationType,
            ParentEntity = r.ParentEntity,
            ChildEntity = r.ChildEntity,
            ForeignKey = r.ForeignKey,
            NavigationProperty = r.NavigationProperty,
            TabLabel = r.TabLabel,
            DisplayOrder = r.DisplayOrder
        }).ToList()
    };

    private sealed record HealthCheckContext(
        IReadOnlyList<ForgeForm> Forms,
        HashSet<string> PermissionCodes,
        HashSet<string> LookupEntities,
        HashSet<int> MenuFormIds,
        IReadOnlyList<EntityMetadataDto> Discovered,
        Dictionary<string, EntityMetadataDto> DiscoveredByEntity,
        HashSet<string> ConfiguredEntities);
}
