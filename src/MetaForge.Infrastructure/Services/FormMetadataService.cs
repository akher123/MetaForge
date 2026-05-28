namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Builds form definitions from cached admin metadata.
/// </summary>
public class FormMetadataService : IFormMetadataService
{
    private readonly IFormMetadataCache _formCache;

    public FormMetadataService(IFormMetadataCache formCache) =>
        _formCache = formCache;

    public async Task<FormDefinition?> GetFormDefinitionAsync(string formCode, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken);
        return form == null ? null : MapForm(form);
    }

    public async Task<FormDefinition?> GetFormDefinitionByEntityAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        return form == null ? null : MapForm(form);
    }

    public Task InvalidateCacheAsync(string formCode, string? entityName = null, CancellationToken cancellationToken = default)
    {
        _formCache.Invalidate(formCode, entityName);
        return Task.CompletedTask;
    }

    internal static FormDefinition MapForm(Domain.Metadata.ForgeForm form) => new()
    {
        FormId = form.Id,
        FormCode = form.Code,
        FormName = form.Name,
        FormType = form.FormType.ToString(),
        EntityName = form.EntityName,
        Fields = form.Fields.Select(f => new FieldDefinition
        {
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
            SectionName = f.SectionName
        }).OrderBy(f => f.DisplayOrder).ToList(),
        Relations = form.Relations.Select(r => new RelationDefinition
        {
            RelationType = r.RelationType,
            ParentEntity = r.ParentEntity,
            ChildEntity = r.ChildEntity,
            ForeignKey = r.ForeignKey,
            NavigationProperty = r.NavigationProperty,
            TabLabel = r.TabLabel,
            DisplayOrder = r.DisplayOrder
        }).ToList()
    };
}
