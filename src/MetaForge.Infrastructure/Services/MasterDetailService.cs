using System.Linq.Expressions;
using System.Text.Json;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Shared.Constants;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Generic master-detail screen engine (single inline detail or tabular multi-detail).
/// </summary>
public class MasterDetailService : IMasterDetailService
{
    private readonly IFormMetadataCache _formCache;
    private readonly IFormMetadataService _formMetadataService;
    private readonly IGridService _gridService;
    private readonly IGenericCrudService _crudService;
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IAuditService _auditService;
    private readonly IDynamicValidationService _validationService;

    public MasterDetailService(
        IFormMetadataCache formCache,
        IFormMetadataService formMetadataService,
        IGridService gridService,
        IGenericCrudService crudService,
        MetaForgeDbContext dbContext,
        IEntityTypeResolver typeResolver,
        IAuditService auditService,
        IDynamicValidationService validationService)
    {
        _formCache = formCache;
        _formMetadataService = formMetadataService;
        _gridService = gridService;
        _crudService = crudService;
        _dbContext = dbContext;
        _typeResolver = typeResolver;
        _auditService = auditService;
        _validationService = validationService;
    }

    public async Task<MasterDetailScreenDto> LoadScreenAsync(string formCode, object? masterId = null, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        var masterForm = await _formMetadataService.GetFormDefinitionAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form for form '{formCode}' was not found.");

        var isTabular = form.FormType == FormType.MasterDetailTabular;
        var screen = new MasterDetailScreenDto
        {
            ScreenMode = isTabular ? "Tabular" : "Single",
            MasterForm = masterForm
        };

        if (isTabular)
        {
            screen.DetailSections = await BuildDetailSectionsAsync(form, masterForm, masterId, cancellationToken);
            if (screen.DetailSections.Count > 0)
            {
                var first = screen.DetailSections[0];
                screen.DetailForm = first.DetailForm;
                screen.DetailRelation = first.Relation;
                screen.DetailGrid = first.DetailGrid;
                screen.DetailData = first.DetailData;
            }
        }
        else
        {
            var detailRelation = masterForm.Relations.FirstOrDefault(r => r.RelationType == RelationType.OneToMany)
                ?? throw new NotFoundException($"No OneToMany relation configured for form '{formCode}'.");

            var section = await BuildDetailSectionAsync(form, detailRelation, masterId, cancellationToken);
            screen.DetailForm = section.DetailForm;
            screen.DetailRelation = section.Relation;
            screen.DetailGrid = section.DetailGrid;
            screen.DetailData = section.DetailData;
            screen.DetailSections = [section];
        }

        if (masterId != null)
            screen.MasterData = await LoadMasterAsync(formCode, masterId, cancellationToken);

        return screen;
    }

    public Task<Dictionary<string, object?>> LoadMasterAsync(string formCode, object masterId, CancellationToken cancellationToken = default)
    {
        return GetMasterEntityName(formCode).ContinueWith(async t =>
        {
            var entityName = await t;
            return await _crudService.GetByIdAsync(entityName, masterId, cancellationToken);
        }, cancellationToken).Unwrap();
    }

    public async Task<List<Dictionary<string, object?>>> LoadDetailsAsync(string formCode, object masterId, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        var relation = form.Relations.FirstOrDefault(r => r.RelationType == RelationType.OneToMany)
            ?? throw new NotFoundException($"No OneToMany relation for form '{formCode}'.");

        return await LoadDetailRowsAsync(relation.ChildEntity, relation.ForeignKey, masterId, cancellationToken);
    }

    public async Task<object> SaveMasterDetailAsync(
        string formCode,
        Dictionary<string, object?> masterData,
        List<Dictionary<string, object?>>? detailData,
        IReadOnlyList<int>? deletedDetailIds = null,
        IReadOnlyList<DetailSectionSaveDto>? detailSections = null,
        CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        masterData = DynamicEntityMapper.NormalizeDictionary(masterData);
        detailData = detailData?.Select(DynamicEntityMapper.NormalizeDictionary).ToList();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        object masterId;
        if (masterData.TryGetValue("Id", out var idVal) && idVal != null && DynamicEntityMapper.ToInt32(idVal) > 0)
        {
            masterId = DynamicEntityMapper.ToInt32(idVal);
            await _crudService.UpdateAsync(form.EntityName, masterId, masterData, cancellationToken);
        }
        else
        {
            var created = await _crudService.CreateAsync(form.EntityName, masterData, cancellationToken);
            masterId = created.GetType().GetProperty("Id")!.GetValue(created)!;
        }

        if (detailSections != null && detailSections.Count > 0)
        {
            foreach (var section in detailSections)
            {
                var relation = form.Relations.FirstOrDefault(r =>
                    r.RelationType == RelationType.OneToMany
                    && r.ChildEntity.Equals(section.ChildEntity, StringComparison.OrdinalIgnoreCase));

                if (relation == null) continue;

                if (section.DeletedIds.Count > 0)
                {
                    foreach (var detailId in section.DeletedIds.Distinct().Where(id => id > 0))
                        await _crudService.DeleteAsync(relation.ChildEntity, detailId, cancellationToken);
                }

                foreach (var detail in section.Rows.Select(DynamicEntityMapper.NormalizeDictionary))
                {
                    detail[relation.ForeignKey] = masterId;
                    if (detail.TryGetValue("Id", out var detailId) && detailId != null && DynamicEntityMapper.ToInt32(detailId) > 0)
                    {
                        var parsedDetailId = DynamicEntityMapper.ToInt32(detailId);
                        await _validationService.ValidateAsync(relation.ChildEntity, detail, cancellationToken);
                        await _crudService.UpdateAsync(relation.ChildEntity, parsedDetailId, detail, cancellationToken);
                    }
                    else
                    {
                        detail.Remove("Id");
                        await _validationService.ValidateAsync(relation.ChildEntity, detail, cancellationToken);
                        await _crudService.CreateAsync(relation.ChildEntity, detail, cancellationToken);
                    }
                }
            }
        }
        else
        {
            var relation = form.Relations.FirstOrDefault(r => r.RelationType == RelationType.OneToMany);

            if (relation != null && deletedDetailIds != null)
            {
                foreach (var detailId in deletedDetailIds.Distinct().Where(id => id > 0))
                    await _crudService.DeleteAsync(relation.ChildEntity, detailId, cancellationToken);
            }

            if (detailData != null && relation != null)
            {
                foreach (var detail in detailData)
                {
                    detail[relation.ForeignKey] = masterId;

                    if (detail.TryGetValue("Id", out var detailId) && detailId != null && DynamicEntityMapper.ToInt32(detailId) > 0)
                    {
                        var parsedDetailId = DynamicEntityMapper.ToInt32(detailId);
                        await _validationService.ValidateAsync(relation.ChildEntity, detail, cancellationToken);
                        await _crudService.UpdateAsync(relation.ChildEntity, parsedDetailId, detail, cancellationToken);
                    }
                    else
                    {
                        detail.Remove("Id");
                        await _validationService.ValidateAsync(relation.ChildEntity, detail, cancellationToken);
                        await _crudService.CreateAsync(relation.ChildEntity, detail, cancellationToken);
                    }
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        await _auditService.LogAsync(
            form.EntityName,
            masterId.ToString()!,
            "SaveMasterDetail",
            null,
            JsonSerializer.Serialize(new { masterData, detailData, deletedDetailIds, detailSections }),
            cancellationToken);

        return masterId;
    }

    public async Task DeleteDetailAsync(string formCode, object detailId, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        var relation = form.Relations.FirstOrDefault(r => r.RelationType == RelationType.OneToMany)
            ?? throw new NotFoundException($"No OneToMany relation for form '{formCode}'.");

        await _crudService.DeleteAsync(relation.ChildEntity, detailId, cancellationToken);
    }

    private async Task<List<DetailSectionDto>> BuildDetailSectionsAsync(
        ForgeForm form,
        FormDefinition masterForm,
        object? masterId,
        CancellationToken cancellationToken)
    {
        var relations = masterForm.Relations
            .Where(r => r.RelationType == RelationType.OneToMany)
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.ChildEntity)
            .ToList();

        if (relations.Count == 0)
            throw new NotFoundException($"No OneToMany relations configured for tabular form '{form.Code}'.");

        var sections = new List<DetailSectionDto>();
        foreach (var relation in relations)
            sections.Add(await BuildDetailSectionAsync(form, relation, masterId, cancellationToken));

        return sections;
    }

    private async Task<DetailSectionDto> BuildDetailSectionAsync(
        ForgeForm form,
        RelationDefinition detailRelation,
        object? masterId,
        CancellationToken cancellationToken)
    {
        var detailForm = await _formMetadataService.GetFormDefinitionByEntityAsync(detailRelation.ChildEntity, cancellationToken)
            ?? new FormDefinition { EntityName = detailRelation.ChildEntity };

        detailForm = BuildDetailForm(detailForm, detailRelation.ForeignKey);

        var detailModule = await _formCache.GetByEntityNameAsync(detailRelation.ChildEntity, cancellationToken);
        var detailGrid = detailModule != null
            ? await _gridService.GetGridDefinitionAsync(detailModule.Code, cancellationToken) ?? new GridDefinition { Entity = detailRelation.ChildEntity }
            : new GridDefinition { Entity = detailRelation.ChildEntity };

        detailGrid = BuildDetailGrid(detailGrid, detailForm, detailRelation.ForeignKey);

        var tabLabel = !string.IsNullOrWhiteSpace(detailRelation.TabLabel)
            ? detailRelation.TabLabel!
            : detailForm.FormName is { Length: > 0 } name && name != detailRelation.ChildEntity
                ? detailForm.FormName
                : SplitPascalCase(detailRelation.ChildEntity);

        var section = new DetailSectionDto
        {
            ChildEntity = detailRelation.ChildEntity,
            ForeignKey = detailRelation.ForeignKey,
            TabLabel = tabLabel,
            DisplayOrder = detailRelation.DisplayOrder,
            DetailForm = detailForm,
            Relation = detailRelation,
            DetailGrid = detailGrid
        };

        if (masterId != null)
            section.DetailData = await LoadDetailRowsAsync(detailRelation.ChildEntity, detailRelation.ForeignKey, masterId, cancellationToken);

        return section;
    }

    private async Task<List<Dictionary<string, object?>>> LoadDetailRowsAsync(
        string childEntity,
        string foreignKey,
        object masterId,
        CancellationToken cancellationToken)
    {
        var childType = _typeResolver.Resolve(childEntity);
        var masterIdValue = DynamicEntityMapper.ToInt32(masterId);
        var items = await QueryDetailsByForeignKeyAsync(childType, foreignKey, masterIdValue, cancellationToken);
        return items.Select(i => DynamicEntityMapper.ToDictionary(i)).ToList();
    }

    private static FormDefinition BuildDetailForm(FormDefinition detailForm, string foreignKey)
    {
        return new FormDefinition
        {
            FormId = detailForm.FormId,
            FormCode = detailForm.FormCode,
            FormName = detailForm.FormName,
            EntityName = detailForm.EntityName,
            Fields = detailForm.Fields
                .Where(f => !IsInternalDetailField(f.PropertyName, foreignKey))
                .OrderBy(f => f.DisplayOrder)
                .ToList(),
            Relations = detailForm.Relations
        };
    }

    private static GridDefinition BuildDetailGrid(GridDefinition detailGrid, FormDefinition detailForm, string foreignKey)
    {
        if (detailForm.Fields.Count == 0)
        {
            detailGrid.Columns = detailGrid.Columns
                .Where(c => !IsInternalDetailField(c.PropertyName, foreignKey))
                .ToList();
            return detailGrid;
        }

        detailGrid.Columns = detailForm.Fields.Select(f => new GridColumnDefinition
        {
            PropertyName = f.PropertyName,
            Label = f.Label,
            IsSortable = false,
            IsSearchable = false,
            IsVisible = f.IsVisible,
            ControlType = f.ControlType,
            LookupEntity = f.LookupEntity,
            LookupParentField = f.LookupParentField,
            LookupFilterField = f.LookupFilterField,
            DisplayFormat = GridDisplayFormats.GetDefaultForControlType(f.ControlType)
        }).ToList();

        return detailGrid;
    }

    private static bool IsInternalDetailField(string propertyName, string foreignKey) =>
        propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals(foreignKey, StringComparison.OrdinalIgnoreCase);

    private static string SplitPascalCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    private async Task<List<object>> QueryDetailsByForeignKeyAsync(
        Type childType,
        string foreignKey,
        int masterIdValue,
        CancellationToken cancellationToken)
    {
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(childType);
        var dbSet = setMethod.Invoke(_dbContext, null)!;

        var parameter = Expression.Parameter(childType, "e");
        var fkProperty = Expression.Property(parameter, foreignKey);
        var constant = Expression.Constant(masterIdValue);
        var equality = Expression.Equal(fkProperty, Expression.Convert(constant, fkProperty.Type));
        var lambda = Expression.Lambda(equality, parameter);

        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(childType);

        var filtered = (IQueryable)whereMethod.Invoke(null, [dbSet, lambda])!;

        var toListMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
            .MakeGenericMethod(childType);

        var task = (Task)toListMethod.Invoke(null, [filtered, cancellationToken])!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result")!;
        return ((System.Collections.IEnumerable)resultProperty.GetValue(task)!).Cast<object>().ToList();
    }

    private async Task<string> GetMasterEntityName(string formCode)
    {
        var form = await _formCache.GetByCodeAsync(formCode)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");
        return form.EntityName;
    }
}
