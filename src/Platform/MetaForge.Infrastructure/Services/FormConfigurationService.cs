namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Creates and manages admin form metadata for master data and transaction screens.
/// </summary>
public class FormConfigurationService : IFormConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEntityMetadataDiscoveryService _discoveryService;
    private readonly IFormMetadataService _formMetadataService;
    private readonly ISecurityManagementService _securityManagementService;
    private readonly IMenuSyncService _menuSyncService;

    public FormConfigurationService(
        IUnitOfWork unitOfWork,
        IEntityMetadataDiscoveryService discoveryService,
        IFormMetadataService formMetadataService,
        ISecurityManagementService securityManagementService,
        IMenuSyncService menuSyncService)
    {
        _unitOfWork = unitOfWork;
        _discoveryService = discoveryService;
        _formMetadataService = formMetadataService;
        _securityManagementService = securityManagementService;
        _menuSyncService = menuSyncService;
    }

    public async Task<IReadOnlyList<FormConfigListItemDto>> GetAllFormsAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _unitOfWork.Forms.GetAllAsync(cancellationToken);
        return modules
            .OrderBy(m => m.GroupName)
            .ThenBy(m => m.DisplayOrder)
            .Select(m => new FormConfigListItemDto
            {
                Id = m.Id,
                Code = m.Code,
                Name = m.Name,
                EntityName = m.EntityName,
                GroupName = m.GroupName ?? "General",
                FormType = m.FormType.ToString(),
                IsActive = m.IsActive,
                FieldCount = m.Fields?.Count ?? 0,
                Url = $"/Modules/{m.Code}"
            }).ToList();
    }

    public async Task<FormConfigDto?> GetFormAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _unitOfWork.Forms.GetByIdAsync(id, cancellationToken);
        return module == null ? null : MapToDto(module);
    }

    public async Task<FormConfigDto?> GetFormByEntityAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var module = await _unitOfWork.Forms.GetByEntityNameAsync(entityName, cancellationToken);
        return module == null ? null : MapToDto(module);
    }

    public async Task<FormBuilderScreenDto> GetScreenAsync(int id, CancellationToken cancellationToken = default)
    {
        var master = await GetFormAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Form {id} was not found.");

        var detailRelation = master.Relations
            .FirstOrDefault(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase));

        FormConfigDto? detail = null;
        if (detailRelation != null)
        {
            detail = await GetFormByEntityAsync(detailRelation.ChildEntity, cancellationToken)
                ?? await BuildDraftAsync(detailRelation.ChildEntity, master.GroupName, cancellationToken);

            ApplyDetailFormDefaults(detail, detailRelation.ForeignKey);
        }

        return new FormBuilderScreenDto
        {
            ScreenType = master.FormType.Equals(FormType.MasterDetailTabular.ToString(), StringComparison.OrdinalIgnoreCase)
                ? "MasterDetailTabular"
                : master.Relations.Any(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase))
                    ? "MasterDetail"
                    : "Master",
            Master = master,
            Detail = detail
        };
    }

    public async Task<IReadOnlyList<DiscoveredEntityOptionDto>> GetDiscoveredEntitiesAsync(CancellationToken cancellationToken = default)
    {
        var discovered = _discoveryService.DiscoverAll();
        var configured = (await _unitOfWork.Forms.GetAllAsync(cancellationToken))
            .Select(m => m.EntityName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return discovered.Select(d => new DiscoveredEntityOptionDto
        {
            EntityName = d.EntityName,
            TableName = d.TableName,
            IsConfigured = configured.Contains(d.EntityName),
            Metadata = d
        }).ToList();
    }

    public Task<FormConfigDto> BuildDraftAsync(string entityName, string groupName, CancellationToken cancellationToken = default)
    {
        var metadata = _discoveryService.Discover(entityName)
            ?? throw new NotFoundException($"Entity '{entityName}' was not found.");

        var draft = new FormConfigDto
        {
            Code = entityName.ToLowerInvariant(),
            Name = SplitPascalCase(entityName),
            EntityName = entityName,
            TableName = metadata.TableName,
            GroupName = NormalizeGroupName(groupName),
            DisplayOrder = 0,
            IsActive = true,
            Fields = metadata.Properties
                .Where(p => !p.IsKey && p.Name != "Id")
                .Select((p, i) => new FormFieldConfigDto
                {
                    PropertyName = p.Name,
                    Label = SplitPascalCase(p.Name),
                    ControlType = InferControlType(p.ClrType, p.Name),
                    IsRequired = !p.IsNullable && !p.IsForeignKey,
                    IsVisible = true,
                    DisplayOrder = i,
                    LookupEntity = p.IsForeignKey ? p.Name.Replace("Id", "", StringComparison.Ordinal) : null,
                    ValidationRule = p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "Email" : null
                }).ToList(),
            GridColumns = metadata.Properties
                .Where(p => p.IsKey || !p.IsForeignKey || p.Name.EndsWith("Id"))
                .Take(6)
                .Select((p, i) => new FormGridColumnConfigDto
                {
                    PropertyName = p.Name,
                    Label = SplitPascalCase(p.Name),
                    DisplayOrder = i,
                    IsSortable = true,
                    IsSearchable = p.ClrType.Contains("String", StringComparison.Ordinal),
                    IsVisible = p.Name != "Id"
                }).ToList(),
            Relations = metadata.Relations.Select(r => new FormRelationConfigDto
            {
                RelationType = r.RelationType,
                ParentEntity = r.ParentEntity,
                ChildEntity = r.ChildEntity,
                ForeignKey = r.ForeignKey,
                NavigationProperty = r.NavigationProperty
            }).ToList()
        };

        return Task.FromResult(draft);
    }

    public async Task<int> SaveFormAsync(FormConfigDto config, CancellationToken cancellationToken = default)
    {
        EnsureGridColumns(config);
        Validate(config);

        if (await _unitOfWork.Forms.ExistsByCodeAsync(config.Code, config.Id > 0 ? config.Id : null, cancellationToken))
            throw new BusinessException($"Form code '{config.Code}' already exists.");

        if (await _unitOfWork.Forms.ExistsByEntityNameAsync(config.EntityName, config.Id > 0 ? config.Id : null, cancellationToken))
            throw new BusinessException($"Entity '{config.EntityName}' is already configured.");

        ForgeForm module;
        string? previousEntityName = null;

        if (config.Id > 0)
        {
            module = await _unitOfWork.Forms.GetByIdTrackedAsync(config.Id, cancellationToken)
                ?? throw new NotFoundException($"Form {config.Id} was not found.");

            previousEntityName = module.EntityName;
            module.Fields.Clear();
            module.Relations.Clear();
            module.GridColumns.Clear();
        }
        else
        {
            module = new ForgeForm();
            await _unitOfWork.Forms.AddAsync(module, cancellationToken);
        }

        module.Code = config.Code.Trim().ToLowerInvariant();
        module.Name = config.Name.Trim();
        module.EntityName = config.EntityName.Trim();
        module.TableName = config.TableName.Trim();
        module.GroupName = NormalizeGroupName(config.GroupName);
        module.FormType = ParseFormType(config.FormType);
        module.DisplayOrder = config.DisplayOrder;
        module.IsActive = config.IsActive;

        module.Fields = config.Fields.Select((f, i) => new ForgeField
        {
            PropertyName = f.PropertyName.Trim(),
            Label = f.Label.Trim(),
            ControlType = f.ControlType,
            IsRequired = f.IsRequired,
            IsVisible = f.IsVisible,
            IsReadOnly = f.IsReadOnly,
            DisplayOrder = f.DisplayOrder >= 0 ? f.DisplayOrder : i,
            ValidationRule = f.ValidationRule,
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
            SectionName = f.SectionName
        }).ToList();

        module.GridColumns = config.GridColumns.Select((c, i) => new ForgeGridColumn
        {
            PropertyName = c.PropertyName.Trim(),
            Label = c.Label.Trim(),
            DisplayOrder = c.DisplayOrder >= 0 ? c.DisplayOrder : i,
            IsSortable = c.IsSortable,
            IsSearchable = c.IsSearchable,
            IsVisible = c.IsVisible
        }).ToList();

        module.Relations = config.Relations.Select((r, i) => new ForgeRelation
        {
            RelationType = r.RelationType,
            ParentEntity = r.ParentEntity.Trim(),
            ChildEntity = r.ChildEntity.Trim(),
            ForeignKey = r.ForeignKey.Trim(),
            NavigationProperty = r.NavigationProperty,
            TabLabel = string.IsNullOrWhiteSpace(r.TabLabel) ? null : r.TabLabel.Trim(),
            DisplayOrder = r.DisplayOrder >= 0 ? r.DisplayOrder : i
        }).ToList();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _formMetadataService.InvalidateCacheAsync(module.Code, module.EntityName, cancellationToken);
        if (!string.IsNullOrEmpty(previousEntityName)
            && !previousEntityName.Equals(module.EntityName, StringComparison.OrdinalIgnoreCase))
        {
            await _formMetadataService.InvalidateCacheAsync(module.Code, previousEntityName, cancellationToken);
        }
        await _securityManagementService.SyncFormPermissionsAsync(cancellationToken);
        await _menuSyncService.SyncFormMenuAsync(module, cancellationToken);

        return module.Id;
    }

    public async Task<int> SaveScreenAsync(FormBuilderSaveDto screen, CancellationToken cancellationToken = default)
    {
        if (screen.Master == null)
            throw new BusinessException("Master form configuration is required.");

        var isMasterDetail = screen.ScreenType.Equals("MasterDetail", StringComparison.OrdinalIgnoreCase);
        var isTabular = screen.ScreenType.Equals("MasterDetailTabular", StringComparison.OrdinalIgnoreCase);

        screen.Master.FormType = isTabular
            ? FormType.MasterDetailTabular.ToString()
            : isMasterDetail
                ? FormType.MasterDetail.ToString()
                : FormType.Master.ToString();

        if ((isMasterDetail || isTabular) && screen.Detail != null)
        {
            var detailRelation = isTabular
                ? screen.Master.Relations
                    .Where(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.DisplayOrder)
                    .FirstOrDefault()
                : screen.Master.Relations
                    .FirstOrDefault(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase));

            if (detailRelation != null)
            {
                screen.Detail.EntityName = detailRelation.ChildEntity;
                screen.Detail.GroupName = screen.Master.GroupName;
                if (string.IsNullOrWhiteSpace(screen.Detail.Code))
                    screen.Detail.Code = detailRelation.ChildEntity.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(screen.Detail.Name))
                    screen.Detail.Name = SplitPascalCase(detailRelation.ChildEntity);

                ApplyDetailFormDefaults(screen.Detail, detailRelation.ForeignKey);
                screen.Detail.FormType = FormType.Detail.ToString();
            }
        }

        var masterId = await SaveFormAsync(screen.Master, cancellationToken);

        if ((isMasterDetail || isTabular) && screen.Detail != null && screen.Detail.Fields.Count > 0)
        {
            if (screen.Detail.Id == 0)
            {
                var existingDetail = await _unitOfWork.Forms.GetByEntityNameAsync(screen.Detail.EntityName, cancellationToken);
                if (existingDetail != null)
                    screen.Detail.Id = existingDetail.Id;
            }

            await SaveFormAsync(screen.Detail, cancellationToken);
        }

        if (isTabular)
        {
            foreach (var relation in screen.Master.Relations
                         .Where(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase)))
            {
                var existingDetail = await _unitOfWork.Forms.GetByEntityNameAsync(relation.ChildEntity, cancellationToken);
                if (existingDetail != null)
                    continue;

                var draft = await BuildDraftAsync(relation.ChildEntity, screen.Master.GroupName, cancellationToken);
                ApplyDetailFormDefaults(draft, relation.ForeignKey);
                draft.FormType = FormType.Detail.ToString();
                draft.Code = relation.ChildEntity.ToLowerInvariant();
                draft.Name = !string.IsNullOrWhiteSpace(relation.TabLabel)
                    ? relation.TabLabel!
                    : SplitPascalCase(relation.ChildEntity);
                draft.GroupName = screen.Master.GroupName;
                draft.DisplayOrder = screen.Master.DisplayOrder + relation.DisplayOrder + 1;

                if (draft.Fields.Count > 0)
                    await SaveFormAsync(draft, cancellationToken);
            }
        }

        return masterId;
    }

    public async Task DeleteFormAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _unitOfWork.Forms.GetByIdTrackedAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Form {id} was not found.");

        _unitOfWork.Forms.Remove(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _formMetadataService.InvalidateCacheAsync(module.Code, module.EntityName, cancellationToken);
        await _menuSyncService.DeactivateFormMenuAsync(module.Id, cancellationToken);
    }

    private static void Validate(FormConfigDto config)
    {
        if (string.IsNullOrWhiteSpace(config.Code))
            throw new BusinessException("Form code is required.");
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new BusinessException("Form name is required.");
        if (string.IsNullOrWhiteSpace(config.EntityName))
            throw new BusinessException("Entity name is required.");
        if (config.Fields.Count == 0)
            throw new BusinessException("At least one field is required.");

        var isDetailForm = config.FormType.Equals(FormType.Detail.ToString(), StringComparison.OrdinalIgnoreCase);
        if (config.GridColumns.Count == 0 && !isDetailForm)
            throw new BusinessException("At least one grid column is required.");
    }

    private static void EnsureGridColumns(FormConfigDto config)
    {
        if (config.GridColumns.Count > 0)
            return;

        config.GridColumns = config.Fields
            .Where(f => f.IsVisible
                && !string.Equals(f.ControlType, ControlType.Hidden, StringComparison.OrdinalIgnoreCase))
            .Select((f, i) => new FormGridColumnConfigDto
            {
                PropertyName = f.PropertyName,
                Label = f.Label,
                DisplayOrder = i,
                IsSortable = false,
                IsSearchable = false,
                IsVisible = true
            })
            .ToList();
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
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
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
            IsVisible = c.IsVisible
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

    private static string NormalizeGroupName(string groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? "Master Data" : groupName.Trim();

    private static string SplitPascalCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    private static void ApplyDetailFormDefaults(FormConfigDto detail, string foreignKey)
    {
        if (string.IsNullOrWhiteSpace(foreignKey)) return;

        foreach (var field in detail.Fields.Where(f =>
            f.PropertyName.Equals(foreignKey, StringComparison.OrdinalIgnoreCase)))
        {
            field.IsVisible = false;
            field.ControlType = ControlType.Hidden;
        }
    }

    private static string InferControlType(string clrType, string propertyName)
    {
        if (propertyName.EndsWith("Id", StringComparison.Ordinal) && propertyName != "Id")
            return ControlType.Autocomplete;
        if (clrType.Contains("Boolean", StringComparison.Ordinal)) return ControlType.Checkbox;
        if (clrType.Contains("DateTime", StringComparison.Ordinal)) return ControlType.DateTime;
        if (clrType.Contains("DateOnly", StringComparison.Ordinal) || propertyName.Contains("Date", StringComparison.Ordinal))
            return ControlType.Date;
        if (clrType.Contains("Int", StringComparison.Ordinal) || clrType.Contains("Decimal", StringComparison.Ordinal) || clrType.Contains("Double", StringComparison.Ordinal))
            return ControlType.Number;
        if (propertyName.Contains("Description", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("Notes", StringComparison.OrdinalIgnoreCase))
            return ControlType.TextArea;
        return ControlType.TextBox;
    }

    private static FormType ParseFormType(string? formType) =>
        Enum.TryParse<FormType>(formType, true, out var parsed) ? parsed : FormType.Master;
}
