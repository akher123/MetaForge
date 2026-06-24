using MetaForge.Application.Validation;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Validation;
using MetaForge.Shared.Constants;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Creates and manages admin form metadata for master data and transaction screens.
/// </summary>
public class FormConfigurationService : IFormConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityMetadataDiscoveryService _discoveryService;
    private readonly IFormMetadataService _formMetadataService;
    private readonly ISecurityManagementService _securityManagementService;
    private readonly IMenuSyncService _menuSyncService;
    private readonly ILookupService _lookupService;

    public FormConfigurationService(
        IUnitOfWork unitOfWork,
        MetaForgeDbContext dbContext,
        IEntityMetadataDiscoveryService discoveryService,
        IFormMetadataService formMetadataService,
        ISecurityManagementService securityManagementService,
        IMenuSyncService menuSyncService,
        ILookupService lookupService)
    {
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _discoveryService = discoveryService;
        _formMetadataService = formMetadataService;
        _securityManagementService = securityManagementService;
        _menuSyncService = menuSyncService;
        _lookupService = lookupService;
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
        if (module == null)
            return null;

        var dto = MapToDto(module);
        await EnrichLookupFieldSettingsAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<FormConfigDto?> GetFormByEntityAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var module = await _unitOfWork.Forms.GetByEntityNameAsync(entityName, cancellationToken);
        if (module == null)
            return null;

        var dto = MapToDto(module);
        await EnrichLookupFieldSettingsAsync(dto, cancellationToken);
        return dto;
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
            ScreenType = ResolveScreenType(master),
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

    public async Task<FormConfigDto> BuildDraftAsync(string entityName, string groupName, CancellationToken cancellationToken = default)
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
                .Select((p, i) =>
                {
                    var lookupEntity = p.IsForeignKey ? p.Name.Replace("Id", "", StringComparison.Ordinal) : null;
                    return new FormFieldConfigDto
                    {
                        PropertyName = p.Name,
                        Label = SplitPascalCase(p.Name),
                        ControlType = InferControlType(p.ClrType, p.Name),
                        IsRequired = !p.IsNullable && !p.IsForeignKey,
                        IsVisible = true,
                        DisplayOrder = i,
                        LookupEntity = lookupEntity,
                        LookupTextField = lookupEntity == null
                            ? null
                            : InferLookupTextFieldForEntity(lookupEntity),
                        LookupValueField = lookupEntity == null
                            ? null
                            : LookupFieldResolver.DefaultValueField,
                        ValidationRule = p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
                            ? FieldValidationRuleEngine.Serialize(new FieldValidationRuleSet
                            {
                                Rules = [new FieldValidationRuleDefinition { Type = ValidationRuleTypes.Email }]
                            })
                            : null
                    };
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

        await EnrichLookupFieldSettingsAsync(draft, cancellationToken);
        AppendInferredMultiSelectFields(draft, metadata);
        EnrichMultiSelectFieldSettings(draft, metadata);
        return draft;
    }

    public async Task<FormSchemaSyncPreviewDto> GetSchemaSyncPreviewAsync(int formId, CancellationToken cancellationToken = default)
    {
        var form = await GetFormAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

        var metadata = _discoveryService.Discover(form.EntityName)
            ?? throw new NotFoundException($"Entity '{form.EntityName}' was not found.");

        var draft = await BuildDraftAsync(form.EntityName, form.GroupName, cancellationToken);
        draft.FormType = form.FormType;

        return FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);
    }

    public async Task<FormSchemaSyncResultDto> ApplySchemaSyncAsync(
        int formId,
        FormSchemaSyncApplyDto request,
        CancellationToken cancellationToken = default)
    {
        var preview = await GetSchemaSyncPreviewAsync(formId, cancellationToken);
        var form = await GetFormAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

        if (request.AcceptedKeys.Count == 0)
            throw new BusinessException("Select at least one change to apply.");

        var merged = FormSchemaSyncPlanner.Apply(form, preview, request.AcceptedKeys);
        await SaveFormAsync(merged, cancellationToken);

        var updated = await GetFormAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found after sync.");

        return new FormSchemaSyncResultDto
        {
            FormId = formId,
            AppliedChangeCount = request.AcceptedKeys.Count,
            Form = updated
        };
    }

    public async Task<int> SaveFormAsync(FormConfigDto config, CancellationToken cancellationToken = default)
    {
        var metadata = _discoveryService.Discover(config.EntityName);
        if (metadata != null)
            EnrichMultiSelectFieldSettings(config, metadata);

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
            module.GridActions.Clear();
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

        foreach (var field in config.Fields.Select((f, i) => new ForgeField
        {
            PropertyName = f.PropertyName.Trim(),
            Label = f.Label.Trim(),
            ControlType = f.ControlType,
            IsRequired = f.IsRequired,
            IsVisible = f.IsVisible,
            IsReadOnly = f.IsReadOnly,
            DisplayOrder = f.DisplayOrder >= 0 ? f.DisplayOrder : i,
            ValidationRule = NormalizeValidationRule(f.ValidationRule),
            ConditionalRule = NormalizeValidationRule(f.ConditionalRule),
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
            MappingEntity = f.MappingEntity,
            MappingParentKey = f.MappingParentKey,
            MappingRelatedKey = f.MappingRelatedKey,
            SectionName = f.SectionName
        }))
        {
            module.Fields.Add(field);
        }

        foreach (var column in config.GridColumns.Select((c, i) => new ForgeGridColumn
        {
            PropertyName = c.PropertyName.Trim(),
            Label = c.Label.Trim(),
            DisplayOrder = c.DisplayOrder >= 0 ? c.DisplayOrder : i,
            IsSortable = c.IsSortable,
            IsSearchable = c.IsSearchable,
            IsVisible = c.IsVisible,
            DisplayFormat = string.IsNullOrWhiteSpace(c.DisplayFormat) ? null : c.DisplayFormat.Trim()
        }))
        {
            module.GridColumns.Add(column);
        }

        foreach (var action in config.GridActions.Select((a, i) => new ForgeFormAction
        {
            Code = a.Code.Trim().ToLowerInvariant(),
            Label = a.Label.Trim(),
            Icon = string.IsNullOrWhiteSpace(a.Icon) ? null : a.Icon.Trim(),
            Placement = NormalizePlacement(a.Placement),
            HandlerType = NormalizeHandlerType(a.HandlerType),
            HandlerTarget = a.HandlerTarget.Trim(),
            HttpMethod = NormalizeHttpMethod(a.HttpMethod),
            RequestBody = string.IsNullOrWhiteSpace(a.RequestBody) ? null : a.RequestBody.Trim(),
            PermissionAction = string.IsNullOrWhiteSpace(a.PermissionAction) ? null : a.PermissionAction.Trim(),
            ConfirmMessage = string.IsNullOrWhiteSpace(a.ConfirmMessage) ? null : a.ConfirmMessage.Trim(),
            ButtonStyle = string.IsNullOrWhiteSpace(a.ButtonStyle) ? "outline-primary" : a.ButtonStyle.Trim(),
            DisplayOrder = a.DisplayOrder >= 0 ? a.DisplayOrder : i,
            IsActive = a.IsActive
        }))
        {
            if (string.IsNullOrWhiteSpace(action.Code) || string.IsNullOrWhiteSpace(action.Label))
                continue;

            module.GridActions.Add(action);
        }

        foreach (var relation in config.Relations.Select((r, i) => new ForgeRelation
        {
            RelationType = r.RelationType,
            ParentEntity = r.ParentEntity.Trim(),
            ChildEntity = r.ChildEntity.Trim(),
            ForeignKey = r.ForeignKey.Trim(),
            NavigationProperty = r.NavigationProperty,
            TabLabel = string.IsNullOrWhiteSpace(r.TabLabel) ? null : r.TabLabel.Trim(),
            DisplayOrder = r.DisplayOrder >= 0 ? r.DisplayOrder : i
        }))
        {
            module.Relations.Add(relation);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SyncLookupConfigurationsAsync(config.Fields, cancellationToken);
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
        var isTabbed = screen.ScreenType.Equals("Tabbed", StringComparison.OrdinalIgnoreCase);

        screen.Master.FormType = isTabular
            ? FormType.MasterDetailTabular.ToString()
            : isMasterDetail
                ? FormType.MasterDetail.ToString()
                : isTabbed
                    ? FormType.Tabbed.ToString()
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

    private async Task EnrichLookupFieldSettingsAsync(FormConfigDto config, CancellationToken cancellationToken)
    {
        var lookupEntities = config.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.LookupEntity))
            .Select(f => f.LookupEntity!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lookupEntities.Count == 0)
            return;

        var configs = await _dbContext.LookupConfigurations
            .AsNoTracking()
            .Where(c => lookupEntities.Contains(c.EntityName) && c.IsActive)
            .ToListAsync(cancellationToken);

        var configByEntity = configs.ToDictionary(c => c.EntityName, StringComparer.OrdinalIgnoreCase);

        foreach (var field in config.Fields.Where(f => !string.IsNullOrWhiteSpace(f.LookupEntity)))
        {
            var entityName = field.LookupEntity!.Trim();
            if (configByEntity.TryGetValue(entityName, out var lookupConfig))
            {
                field.LookupTextField ??= lookupConfig.TextField;
                field.LookupValueField ??= lookupConfig.ValueField;
                continue;
            }

            field.LookupTextField ??= InferLookupTextFieldForEntity(entityName);
            field.LookupValueField ??= LookupFieldResolver.DefaultValueField;
        }
    }

    private async Task SyncLookupConfigurationsAsync(
        IEnumerable<FormFieldConfigDto> fields,
        CancellationToken cancellationToken)
    {
        var lookupFields = fields
            .Where(f => !string.IsNullOrWhiteSpace(f.LookupEntity)
                && IsLookupControlType(f.ControlType))
            .GroupBy(f => f.LookupEntity!.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in lookupFields)
        {
            var entityName = group.Key;
            var sourceField = group.LastOrDefault(f => !string.IsNullOrWhiteSpace(f.LookupTextField))
                ?? group.Last();

            var textField = string.IsNullOrWhiteSpace(sourceField.LookupTextField)
                ? InferLookupTextFieldForEntity(entityName)
                : sourceField.LookupTextField.Trim();

            var valueField = string.IsNullOrWhiteSpace(sourceField.LookupValueField)
                ? LookupFieldResolver.DefaultValueField
                : sourceField.LookupValueField.Trim();

            var existing = await _dbContext.LookupConfigurations
                .FirstOrDefaultAsync(c => c.EntityName == entityName, cancellationToken);

            if (existing == null)
            {
                _dbContext.LookupConfigurations.Add(new LookupConfiguration
                {
                    EntityName = entityName,
                    TextField = textField,
                    ValueField = valueField,
                    IsActive = true
                });
            }
            else
            {
                existing.TextField = textField;
                existing.ValueField = valueField;
                existing.IsActive = true;
            }

            await _lookupService.InvalidateCacheAsync(entityName, cancellationToken);
        }

        if (lookupFields.Any())
            await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private string InferLookupTextFieldForEntity(string entityName)
    {
        var metadata = _discoveryService.Discover(entityName);
        return metadata == null
            ? LookupFieldResolver.DefaultTextField
            : LookupFieldResolver.InferTextField(metadata);
    }

    private static bool IsLookupControlType(string? controlType) =>
        ControlType.IsLookupOrMultiSelect(controlType);

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

        ValidateFieldConfiguration(config.Fields);

        var isDetailForm = config.FormType.Equals(FormType.Detail.ToString(), StringComparison.OrdinalIgnoreCase);
        if (config.GridColumns.Count == 0 && !isDetailForm)
            throw new BusinessException("At least one grid column is required.");
    }

    private static void ValidateFieldConfiguration(IEnumerable<FormFieldConfigDto> fields)
    {
        foreach (var field in fields)
        {
            var isMultiSelect = ControlType.IsMultiSelect(field.ControlType);
            var isSingleLookup = ControlType.IsSingleLookup(field.ControlType);

            if (isMultiSelect)
            {
                if (string.IsNullOrWhiteSpace(field.LookupEntity))
                    throw new BusinessException($"Lookup entity is required for MultiSelect field '{field.PropertyName}'.");
                if (string.IsNullOrWhiteSpace(field.MappingEntity))
                    throw new BusinessException($"Mapping entity is required for MultiSelect field '{field.PropertyName}'.");
                if (string.IsNullOrWhiteSpace(field.MappingParentKey))
                    throw new BusinessException($"Mapping parent key is required for MultiSelect field '{field.PropertyName}'.");
                if (string.IsNullOrWhiteSpace(field.MappingRelatedKey))
                    throw new BusinessException($"Mapping related key is required for MultiSelect field '{field.PropertyName}'.");
            }

            if (isSingleLookup
                && (!string.IsNullOrWhiteSpace(field.MappingEntity)
                    || !string.IsNullOrWhiteSpace(field.MappingParentKey)
                    || !string.IsNullOrWhiteSpace(field.MappingRelatedKey)))
            {
                throw new BusinessException($"Mapping table settings apply only to MultiSelect fields ('{field.PropertyName}').");
            }

            if (!string.IsNullOrWhiteSpace(field.LookupParentField) && !ControlType.IsLookupOrMultiSelect(field.ControlType))
            {
                throw new BusinessException($"Cascade settings apply only to Dropdown, Autocomplete, or MultiSelect fields ('{field.PropertyName}').");
            }
        }
    }

    private static void EnsureGridColumns(FormConfigDto config)
    {
        if (config.GridColumns.Count > 0)
            return;

        config.GridColumns = config.Fields
            .Where(f => f.IsVisible
                && !string.Equals(f.ControlType, ControlType.Hidden, StringComparison.OrdinalIgnoreCase)
                && !ControlType.IsMultiSelect(f.ControlType))
            .Select((f, i) => new FormGridColumnConfigDto
            {
                PropertyName = f.PropertyName,
                Label = f.Label,
                DisplayOrder = i,
                IsSortable = false,
                IsSearchable = false,
                IsVisible = true,
                DisplayFormat = GridDisplayFormats.GetDefaultForControlType(f.ControlType)
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

    private static string NormalizeGroupName(string groupName) =>
        string.IsNullOrWhiteSpace(groupName) ? "Master Data" : groupName.Trim();

    private static string SplitPascalCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    private static string? NormalizeValidationRule(string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
            return null;

        return validationRule.Trim();
    }

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
        if (propertyName.EndsWith("Ids", StringComparison.Ordinal) && propertyName.Length > 3)
            return ControlType.MultiSelect;
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

    private void AppendInferredMultiSelectFields(FormConfigDto config, EntityMetadataDto metadata)
    {
        var existing = config.Fields
            .Select(f => f.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var junctionField in MultiSelectFieldInference.DiscoverJunctionFields(_dbContext, metadata))
        {
            if (existing.Add(junctionField.PropertyName))
            {
                junctionField.DisplayOrder = config.Fields.Count;
                config.Fields.Add(junctionField);
            }
        }
    }

    private void EnrichMultiSelectFieldSettings(FormConfigDto config, EntityMetadataDto metadata)
    {
        foreach (var field in config.Fields.Where(f => ControlType.IsMultiSelect(f.ControlType)))
            MultiSelectFieldInference.ApplyDefaults(field, metadata, _dbContext);
    }

    private static string ResolveScreenType(FormConfigDto master)
    {
        if (master.FormType.Equals(FormType.MasterDetailTabular.ToString(), StringComparison.OrdinalIgnoreCase))
            return "MasterDetailTabular";

        if (master.FormType.Equals(FormType.Tabbed.ToString(), StringComparison.OrdinalIgnoreCase))
            return "Tabbed";

        if (master.Relations.Any(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase)))
            return "MasterDetail";

        return "Master";
    }

    private static FormType ParseFormType(string? formType) =>
        Enum.TryParse<FormType>(formType, true, out var parsed) ? parsed : FormType.Master;

    private static string NormalizePlacement(string? placement) =>
        string.Equals(placement, GridActionPlacement.Toolbar, StringComparison.OrdinalIgnoreCase)
            ? GridActionPlacement.Toolbar
            : GridActionPlacement.Row;

    private static string NormalizeHandlerType(string? handlerType)
    {
        if (string.Equals(handlerType, GridActionHandlerType.Redirect, StringComparison.OrdinalIgnoreCase))
            return GridActionHandlerType.Redirect;
        if (string.Equals(handlerType, GridActionHandlerType.Script, StringComparison.OrdinalIgnoreCase))
            return GridActionHandlerType.Script;
        return GridActionHandlerType.Api;
    }

    private static string NormalizeHttpMethod(string? httpMethod)
    {
        var method = (httpMethod ?? "POST").Trim().ToUpperInvariant();
        return method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" ? method : "POST";
    }
}
