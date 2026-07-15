using MetaForge.Application.Common;
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

        var treeLevels = new List<TreeLevelConfigDto>();
        if (master.FormType.Equals(FormType.TreeViewMultiTable.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var storedLevels = await _unitOfWork.Forms.GetTreeLevelsAsync(id, cancellationToken);
            if (storedLevels.Count > 0)
                treeLevels = await BuildTreeLevelConfigsAsync(storedLevels, master, cancellationToken);
        }

        return new FormBuilderScreenDto
        {
            ScreenType = ResolveScreenType(master),
            Master = master,
            Detail = detail,
            TreeLevels = treeLevels
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

        var preview = await BuildFormSchemaSyncPreviewAsync(form, cancellationToken);

        if (!IsMasterDetailScreen(form))
            return preview;

        preview.IsCascadeSync = true;
        preview.ScreenType = ResolveScreenType(form);
        FormSchemaSyncPlanner.PrefixChanges(preview.Changes, form.EntityName);

        var relations = GetEffectiveOneToManyRelations(form, preview);
        foreach (var relation in relations)
        {
            var childPreview = await BuildChildSchemaSyncPreviewAsync(form, relation, cancellationToken);
            if (childPreview.HasChanges)
                preview.ChildForms.Add(childPreview);
        }

        return preview;
    }

    public async Task<FormSchemaSyncResultDto> ApplySchemaSyncAsync(
        int formId,
        FormSchemaSyncApplyDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.AcceptedKeys.Count == 0)
            throw new BusinessException("Select at least one change to apply.");

        var form = await GetFormAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

        if (!IsMasterDetailScreen(form))
            return await ApplySingleFormSchemaSyncAsync(formId, request, cancellationToken);

        return await ApplyCascadeSchemaSyncAsync(form, request, cancellationToken);
    }

    private async Task<FormSchemaSyncPreviewDto> BuildFormSchemaSyncPreviewAsync(
        FormConfigDto form,
        CancellationToken cancellationToken)
    {
        var metadata = _discoveryService.Discover(form.EntityName)
            ?? throw new NotFoundException($"Entity '{form.EntityName}' was not found.");

        var draft = await BuildDraftAsync(form.EntityName, form.GroupName, cancellationToken);
        draft.FormType = form.FormType;

        return FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);
    }

    private async Task<FormSchemaSyncChildPreviewDto> BuildChildSchemaSyncPreviewAsync(
        FormConfigDto masterForm,
        FormRelationConfigDto relation,
        CancellationToken cancellationToken)
    {
        var metadata = _discoveryService.Discover(relation.ChildEntity);
        if (metadata == null)
        {
            return new FormSchemaSyncChildPreviewDto
            {
                FormId = 0,
                EntityName = relation.ChildEntity,
                FormName = !string.IsNullOrWhiteSpace(relation.TabLabel)
                    ? relation.TabLabel!
                    : SplitPascalCase(relation.ChildEntity),
                TabLabel = relation.TabLabel,
                ForeignKey = relation.ForeignKey
            };
        }

        var existingChild = await GetFormByEntityAsync(relation.ChildEntity, cancellationToken);
        var childForm = existingChild ?? CreateEmptyChildFormShell(masterForm, relation);
        var draft = await BuildDraftAsync(relation.ChildEntity, masterForm.GroupName, cancellationToken);
        draft.FormType = FormType.Detail.ToString();
        ApplyDetailFormDefaults(childForm, relation.ForeignKey);
        ApplyDetailFormDefaults(draft, relation.ForeignKey);

        var preview = FormSchemaSyncPlanner.BuildPreview(childForm, metadata, draft);
        FormSchemaSyncPlanner.PrefixChanges(preview.Changes, relation.ChildEntity);

        return new FormSchemaSyncChildPreviewDto
        {
            FormId = childForm.Id,
            EntityName = relation.ChildEntity,
            FormName = childForm.Name,
            TabLabel = relation.TabLabel,
            ForeignKey = relation.ForeignKey,
            IsNewForm = existingChild == null,
            Changes = preview.Changes
        };
    }

    private async Task<FormSchemaSyncResultDto> ApplySingleFormSchemaSyncAsync(
        int formId,
        FormSchemaSyncApplyDto request,
        CancellationToken cancellationToken)
    {
        var preview = await BuildFormSchemaSyncPreviewAsync(
            await GetFormAsync(formId, cancellationToken)
                ?? throw new NotFoundException($"Form {formId} was not found."),
            cancellationToken);
        var form = await GetFormAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

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

    private async Task<FormSchemaSyncResultDto> ApplyCascadeSchemaSyncAsync(
        FormConfigDto masterForm,
        FormSchemaSyncApplyDto request,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction == null)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await ApplyCascadeSchemaSyncCoreAsync(masterForm, request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        return await ApplyCascadeSchemaSyncCoreAsync(masterForm, request, cancellationToken);
    }

    private async Task<FormSchemaSyncResultDto> ApplyCascadeSchemaSyncCoreAsync(
        FormConfigDto masterForm,
        FormSchemaSyncApplyDto request,
        CancellationToken cancellationToken)
    {
        var groupedKeys = GroupAcceptedKeys(masterForm.EntityName, request.AcceptedKeys, isCascadeSync: true);

        var masterKeys = groupedKeys.GetValueOrDefault(masterForm.EntityName) ?? [];
        var masterPreview = await BuildFormSchemaSyncPreviewAsync(masterForm, cancellationToken);
        var mergedMaster = FormSchemaSyncPlanner.Apply(masterForm, masterPreview, masterKeys);
        await SaveFormAsync(mergedMaster, cancellationToken);

        var updatedMaster = await GetFormAsync(masterForm.Id, cancellationToken)
            ?? throw new NotFoundException($"Form {masterForm.Id} was not found after sync.");

        var childResults = new List<FormSchemaSyncChildResultDto>();
        var relations = updatedMaster.Relations
            .Where(r => r.RelationType.Equals(RelationType.OneToMany.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.ChildEntity, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var relation in relations)
        {
            if (!groupedKeys.TryGetValue(relation.ChildEntity, out var childKeys) || childKeys.Count == 0)
                continue;

            var childResult = await ApplyChildSchemaSyncAsync(updatedMaster, relation, childKeys, cancellationToken);
            if (childResult != null)
                childResults.Add(childResult);
        }

        return new FormSchemaSyncResultDto
        {
            FormId = masterForm.Id,
            AppliedChangeCount = request.AcceptedKeys.Count,
            Form = updatedMaster,
            IsCascadeSync = true,
            ChildForms = childResults
        };
    }

    private async Task<FormSchemaSyncChildResultDto?> ApplyChildSchemaSyncAsync(
        FormConfigDto masterForm,
        FormRelationConfigDto relation,
        IReadOnlyList<string> acceptedKeys,
        CancellationToken cancellationToken)
    {
        var metadata = _discoveryService.Discover(relation.ChildEntity);
        if (metadata == null)
            return null;

        var existingChild = await GetFormByEntityAsync(relation.ChildEntity, cancellationToken);
        var wasCreated = existingChild == null;
        var childForm = existingChild ?? CreateEmptyChildFormShell(masterForm, relation);

        var draft = await BuildDraftAsync(relation.ChildEntity, masterForm.GroupName, cancellationToken);
        draft.FormType = FormType.Detail.ToString();
        ApplyDetailFormDefaults(childForm, relation.ForeignKey);
        ApplyDetailFormDefaults(draft, relation.ForeignKey);

        var childPreview = FormSchemaSyncPlanner.BuildPreview(childForm, metadata, draft);
        var mergedChild = FormSchemaSyncPlanner.Apply(childForm, childPreview, acceptedKeys);
        ApplyDetailFormDefaults(mergedChild, relation.ForeignKey);

        if (mergedChild.Id == 0)
        {
            mergedChild.FormType = FormType.Detail.ToString();
            mergedChild.TableName = metadata.TableName;
            mergedChild.GroupName = masterForm.GroupName;
            mergedChild.DisplayOrder = masterForm.DisplayOrder + relation.DisplayOrder + 1;
            if (string.IsNullOrWhiteSpace(mergedChild.Code))
                mergedChild.Code = relation.ChildEntity.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(mergedChild.Name))
            {
                mergedChild.Name = !string.IsNullOrWhiteSpace(relation.TabLabel)
                    ? relation.TabLabel!
                    : SplitPascalCase(relation.ChildEntity);
            }
        }

        await SaveFormAsync(mergedChild, cancellationToken);

        var updatedChild = await GetFormByEntityAsync(relation.ChildEntity, cancellationToken)
            ?? throw new NotFoundException($"Detail form '{relation.ChildEntity}' was not found after sync.");

        return new FormSchemaSyncChildResultDto
        {
            FormId = updatedChild.Id,
            EntityName = relation.ChildEntity,
            AppliedChangeCount = acceptedKeys.Count,
            WasCreated = wasCreated,
            Form = updatedChild
        };
    }

    private static bool IsMasterDetailScreen(FormConfigDto form) =>
        form.FormType.Equals(FormType.MasterDetail.ToString(), StringComparison.OrdinalIgnoreCase)
        || form.FormType.Equals(FormType.MasterDetailTabular.ToString(), StringComparison.OrdinalIgnoreCase);

    private static FormConfigDto CreateEmptyChildFormShell(FormConfigDto masterForm, FormRelationConfigDto relation) =>
        new()
        {
            Code = relation.ChildEntity.ToLowerInvariant(),
            Name = !string.IsNullOrWhiteSpace(relation.TabLabel)
                ? relation.TabLabel!
                : SplitPascalCase(relation.ChildEntity),
            EntityName = relation.ChildEntity,
            TableName = relation.ChildEntity + "s",
            GroupName = masterForm.GroupName,
            FormType = FormType.Detail.ToString(),
            DisplayOrder = masterForm.DisplayOrder + relation.DisplayOrder + 1,
            IsActive = true
        };

    private static List<FormRelationConfigDto> GetEffectiveOneToManyRelations(
        FormConfigDto form,
        FormSchemaSyncPreviewDto preview)
    {
        var relations = form.Relations
            .Where(r => r.RelationType.Equals(RelationType.OneToMany.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(CloneRelationForSync)
            .ToList();

        foreach (var change in preview.Changes)
        {
            if (!change.Target.Equals(FormSchemaSyncTargets.Relation, StringComparison.OrdinalIgnoreCase))
                continue;

            if (change.ChangeType == FormSchemaSyncChangeTypes.Add && change.ProposedRelation != null)
            {
                var proposed = change.ProposedRelation;
                if (!proposed.RelationType.Equals(RelationType.OneToMany.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = FormSchemaSyncPlanner.RelationKey(proposed);
                if (relations.Any(r => FormSchemaSyncPlanner.RelationKey(r).Equals(key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                relations.Add(CloneRelationForSync(proposed));
            }
            else if (change.ChangeType == FormSchemaSyncChangeTypes.Remove)
            {
                relations.RemoveAll(r => FormSchemaSyncPlanner.RelationKey(r)
                    .Equals(change.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        return relations
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.ChildEntity, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FormRelationConfigDto CloneRelationForSync(FormRelationConfigDto relation) => new()
    {
        Id = relation.Id,
        RelationType = relation.RelationType,
        ParentEntity = relation.ParentEntity,
        ChildEntity = relation.ChildEntity,
        ForeignKey = relation.ForeignKey,
        NavigationProperty = relation.NavigationProperty,
        TabLabel = relation.TabLabel,
        DisplayOrder = relation.DisplayOrder
    };

    private static Dictionary<string, List<string>> GroupAcceptedKeys(
        string masterEntityName,
        IReadOnlyList<string> acceptedKeys,
        bool isCascadeSync)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in acceptedKeys)
        {
            if (isCascadeSync && FormSchemaSyncPlanner.TryParsePrefixedKey(key, out var entityName, out var localKey))
            {
                groups.TryAdd(entityName, []);
                groups[entityName].Add(localKey);
                continue;
            }

            groups.TryAdd(masterEntityName, []);
            groups[masterEntityName].Add(key);
        }

        return groups;
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

        var isTreeScreen = config.FormType.Equals(FormType.TreeViewMultiTable.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isTreeScreen)
        {
            var existingForms = await _unitOfWork.Forms.GetAllAsync(cancellationToken);
            var conflictingTree = existingForms.FirstOrDefault(f =>
                f.FormType == FormType.TreeViewMultiTable
                && f.EntityName.Equals(config.EntityName, StringComparison.OrdinalIgnoreCase)
                && f.Id != config.Id);
            if (conflictingTree != null)
                throw new BusinessException($"A multi-table tree screen for entity '{config.EntityName}' already exists.");
        }
        else if (await HasEntityNameConflictAsync(config, cancellationToken))
        {
            throw new BusinessException($"Entity '{config.EntityName}' is already configured.");
        }

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

        foreach (var column in config.GridColumns.Select((c, i) =>
        {
            var field = config.Fields.FirstOrDefault(f =>
                string.Equals(f.PropertyName, c.PropertyName, StringComparison.OrdinalIgnoreCase));
            return new ForgeGridColumn
            {
                PropertyName = c.PropertyName.Trim(),
                Label = c.Label.Trim(),
                DisplayOrder = c.DisplayOrder >= 0 ? c.DisplayOrder : i,
                IsSortable = c.IsSortable,
                IsSearchable = c.IsSearchable,
                IsVisible = c.IsVisible,
                DisplayFormat = null
            };
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
        var isTreeMultiTable = screen.ScreenType.Equals("TreeViewMultiTable", StringComparison.OrdinalIgnoreCase);

        if (isTreeMultiTable)
        {
            ValidateTreeLevels(screen.TreeLevels);
            var root = screen.TreeLevels.OrderBy(l => l.LevelIndex).First();
            screen.Master.EntityName = root.EntityName;
            var rootMetadata = _discoveryService.Discover(root.EntityName);
            if (rootMetadata != null && string.IsNullOrWhiteSpace(screen.Master.TableName))
                screen.Master.TableName = rootMetadata.TableName;
        }

        screen.Master.FormType = isTreeMultiTable
            ? FormType.TreeViewMultiTable.ToString()
            : isTabular
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

        if (isTreeMultiTable)
        {
            await SaveTreeLevelsAsync(masterId, screen.TreeLevels, cancellationToken);
            await SaveTreeLevelFormsAsync(screen, cancellationToken);
            return masterId;
        }

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
            ConditionalRule = f.ConditionalRule,
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
            MappingEntity = f.MappingEntity,
            MappingParentKey = f.MappingParentKey,
            MappingRelatedKey = f.MappingRelatedKey,
            SectionName = f.SectionName
        }).ToList(),
        GridColumns = module.GridColumns.OrderBy(c => c.DisplayOrder).Select(c =>
        {
            var field = module.Fields.FirstOrDefault(f =>
                string.Equals(f.PropertyName, c.PropertyName, StringComparison.OrdinalIgnoreCase));
            return new FormGridColumnConfigDto
            {
                Id = c.Id,
                PropertyName = c.PropertyName,
                Label = c.Label,
                DisplayOrder = c.DisplayOrder,
                IsSortable = c.IsSortable,
                IsSearchable = c.IsSearchable,
                IsVisible = c.IsVisible,
                DisplayFormat = null
            };
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

    private async Task<List<TreeLevelConfigDto>> BuildTreeLevelConfigsAsync(
        IEnumerable<ForgeTreeLevel> levels,
        FormConfigDto screenForm,
        CancellationToken cancellationToken)
    {
        var result = new List<TreeLevelConfigDto>();
        foreach (var level in levels.OrderBy(l => l.LevelIndex))
        {
            var dto = new TreeLevelConfigDto
            {
                Id = level.Id,
                LevelIndex = level.LevelIndex,
                EntityName = level.EntityName,
                ParentEntity = level.ParentEntity,
                ForeignKey = level.ForeignKey,
                DisplayColumn = level.DisplayColumn
            };

            if (level.LevelIndex == 0)
            {
                dto.GridColumns = screenForm.GridColumns;
                dto.DisplayColumns = TreeDisplayColumnParser.BuildColumns(level.DisplayColumn, screenForm.GridColumns);
            }
            else
            {
                var entityForm = await GetFormByEntityAsync(level.EntityName, cancellationToken);
                if (entityForm != null)
                {
                    dto.Fields = entityForm.Fields;
                    dto.GridColumns = entityForm.GridColumns;
                    dto.DisplayColumns = TreeDisplayColumnParser.BuildColumns(level.DisplayColumn, entityForm.GridColumns);
                }
                else
                {
                    dto.DisplayColumns = TreeDisplayColumnParser.BuildColumns(level.DisplayColumn);
                }
            }

            result.Add(dto);
        }

        return result;
    }

    private static void ValidateTreeLevels(IReadOnlyList<TreeLevelConfigDto> levels)
    {
        if (levels.Count < 2)
            throw new BusinessException("Multi-table tree requires at least two levels (root and one child).");

        var ordered = levels.OrderBy(l => l.LevelIndex).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];
            if (string.IsNullOrWhiteSpace(level.EntityName))
                throw new BusinessException($"Tree level {i}: entity is required.");

            if (string.IsNullOrWhiteSpace(level.DisplayColumn))
                throw new BusinessException($"Tree level {i}: at least one display column is required.");

            if (i == 0)
                continue;

            if (string.IsNullOrWhiteSpace(level.ForeignKey))
                throw new BusinessException($"Tree level {i}: foreign key is required.");

            var previous = ordered[i - 1];
            if (!string.Equals(level.ParentEntity, previous.EntityName, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException($"Tree level {i}: parent entity must be '{previous.EntityName}'.");
        }
    }

    private async Task SaveTreeLevelsAsync(int formId, List<TreeLevelConfigDto> levels, CancellationToken cancellationToken)
    {
        var module = await _unitOfWork.Forms.GetByIdTrackedAsync(formId, cancellationToken)
            ?? throw new NotFoundException($"Form {formId} was not found.");

        var existing = await _dbContext.ForgeTreeLevels
            .Where(t => t.FormId == formId)
            .ToListAsync(cancellationToken);
        _dbContext.ForgeTreeLevels.RemoveRange(existing);

        foreach (var (level, index) in levels.OrderBy(l => l.LevelIndex).Select((l, i) => (l, i)))
        {
            _dbContext.ForgeTreeLevels.Add(new ForgeTreeLevel
            {
                FormId = formId,
                LevelIndex = level.LevelIndex >= 0 ? level.LevelIndex : index,
                EntityName = level.EntityName.Trim(),
                ParentEntity = string.IsNullOrWhiteSpace(level.ParentEntity) ? null : level.ParentEntity.Trim(),
                ForeignKey = string.IsNullOrWhiteSpace(level.ForeignKey) ? null : level.ForeignKey.Trim(),
                DisplayColumn = string.IsNullOrWhiteSpace(level.DisplayColumn) ? "Name" : level.DisplayColumn.Trim(),
                DisplayOrder = index
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _formMetadataService.InvalidateCacheAsync(module.Code, module.EntityName, cancellationToken);
    }

    private async Task SaveTreeLevelFormsAsync(FormBuilderSaveDto screen, CancellationToken cancellationToken)
    {
        foreach (var level in screen.TreeLevels.Where(l => l.LevelIndex > 0))
        {
            var existing = await _unitOfWork.Forms.GetByEntityNameAsync(level.EntityName, cancellationToken);
            if (existing != null)
                continue;

            FormConfigDto config;
            if (level.Fields.Count > 0 || level.GridColumns.Count > 0)
            {
                var draft = await BuildDraftAsync(level.EntityName, screen.Master.GroupName, cancellationToken);
                config = new FormConfigDto
                {
                    EntityName = level.EntityName,
                    TableName = draft.TableName,
                    Code = level.EntityName.ToLowerInvariant(),
                    Name = SplitPascalCase(level.EntityName),
                    GroupName = screen.Master.GroupName,
                    FormType = FormType.Detail.ToString(),
                    Fields = level.Fields.Count > 0 ? level.Fields : draft.Fields,
                    GridColumns = level.GridColumns.Count > 0 ? level.GridColumns : draft.GridColumns
                };
            }
            else
            {
                config = await BuildDraftAsync(level.EntityName, screen.Master.GroupName, cancellationToken);
                config.FormType = FormType.Detail.ToString();
                config.Code = level.EntityName.ToLowerInvariant();
                config.Name = SplitPascalCase(level.EntityName);
            }

            ApplyDetailFormDefaults(config, level.ForeignKey ?? string.Empty);
            await SaveFormAsync(config, cancellationToken);
        }
    }

    private async Task<bool> HasEntityNameConflictAsync(FormConfigDto config, CancellationToken cancellationToken)
    {
        var excludeId = config.Id > 0 ? config.Id : (int?)null;
        var entityName = config.EntityName.Trim();
        var formType = ParseFormType(config.FormType);

        var others = await _unitOfWork.Forms.GetAllAsync(cancellationToken);
        foreach (var existing in others)
        {
            if (excludeId.HasValue && existing.Id == excludeId.Value)
                continue;

            if (!existing.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (existing.FormType == FormType.TreeViewMultiTable || formType == FormType.TreeViewMultiTable)
                continue;

            if (existing.FormType == FormType.Detail || formType == FormType.Detail)
            {
                if (existing.FormType == FormType.Detail && formType == FormType.Detail)
                    return true;

                continue;
            }

            return true;
        }

        return false;
    }

    private static string ResolveScreenType(FormConfigDto master)
    {
        if (master.FormType.Equals(FormType.TreeViewMultiTable.ToString(), StringComparison.OrdinalIgnoreCase))
            return "TreeViewMultiTable";

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
