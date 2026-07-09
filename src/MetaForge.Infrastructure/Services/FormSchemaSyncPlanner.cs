using MetaForge.Application.DTOs;
using MetaForge.Domain.Enums;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Compares configured form metadata with discovered EF Core entity schema and builds a merge plan.
/// </summary>
public static class FormSchemaSyncPlanner
{
    public static FormSchemaSyncPreviewDto BuildPreview(FormConfigDto form, EntityMetadataDto metadata, FormConfigDto draft)
    {
        var entityProps = GetFormProperties(metadata);
        var entityPropNames = entityProps.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var changes = new List<FormSchemaSyncChangeDto>();
        changes.AddRange(BuildFieldChanges(form, entityProps, draft));
        changes.AddRange(BuildGridColumnChanges(form, entityProps, entityPropNames, draft));
        changes.AddRange(BuildRelationChanges(form, metadata, draft));

        return new FormSchemaSyncPreviewDto
        {
            FormId = form.Id,
            EntityName = form.EntityName,
            FormName = form.Name,
            CurrentFieldCount = form.Fields.Count,
            EntityPropertyCount = entityProps.Count,
            Changes = changes
        };
    }

    public static FormConfigDto Apply(FormConfigDto form, FormSchemaSyncPreviewDto preview, IReadOnlyCollection<string> acceptedKeys)
    {
        var accepted = preview.Changes
            .Where(c => acceptedKeys.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var result = CloneForm(form);

        foreach (var change in accepted)
        {
            switch (change.Target)
            {
                case FormSchemaSyncTargets.Field:
                    ApplyFieldChange(result, change);
                    break;
                case FormSchemaSyncTargets.GridColumn:
                    ApplyGridColumnChange(result, change);
                    break;
                case FormSchemaSyncTargets.Relation:
                    ApplyRelationChange(result, change);
                    break;
            }
        }

        ReorderFields(result);
        ReorderGridColumns(result);
        ReorderRelations(result);

        return result;
    }

    private static IEnumerable<FormSchemaSyncChangeDto> BuildFieldChanges(
        FormConfigDto form,
        IReadOnlyList<EntityPropertyMetadataDto> entityProps,
        FormConfigDto draft)
    {
        var changes = new List<FormSchemaSyncChangeDto>();
        var formFields = form.Fields.ToDictionary(f => f.PropertyName, StringComparer.OrdinalIgnoreCase);
        var draftFields = draft.Fields.ToDictionary(f => f.PropertyName, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in entityProps)
        {
            if (formFields.TryGetValue(prop.Name, out var existing))
            {
                var suggested = draftFields.GetValueOrDefault(prop.Name);
                if (suggested == null) continue;

                var updates = new List<string>();

                if (!string.Equals(existing.ControlType, suggested.ControlType, StringComparison.OrdinalIgnoreCase))
                    updates.Add($"control {existing.ControlType} → {suggested.ControlType}");

                if (existing.IsRequired != suggested.IsRequired)
                    updates.Add($"required {existing.IsRequired} → {suggested.IsRequired}");

                if (updates.Count == 0) continue;

                changes.Add(new FormSchemaSyncChangeDto
                {
                    Key = $"field:{prop.Name}",
                    ChangeType = FormSchemaSyncChangeTypes.Update,
                    Target = FormSchemaSyncTargets.Field,
                    Name = prop.Name,
                    Description = $"Update field settings for '{prop.Name}'.",
                    CurrentSummary = $"Control={existing.ControlType}, Required={existing.IsRequired}",
                    ProposedSummary = $"Control={suggested.ControlType}, Required={suggested.IsRequired}",
                    SelectedByDefault = false,
                    ProposedControlType = suggested.ControlType,
                    ProposedIsRequired = suggested.IsRequired
                });
            }
            else if (draftFields.TryGetValue(prop.Name, out var proposed))
            {
                changes.Add(new FormSchemaSyncChangeDto
                {
                    Key = $"field:{prop.Name}",
                    ChangeType = FormSchemaSyncChangeTypes.Add,
                    Target = FormSchemaSyncTargets.Field,
                    Name = prop.Name,
                    Description = $"Add new entity property '{prop.Name}' to the form.",
                    ProposedSummary = $"Control={proposed.ControlType}, Required={proposed.IsRequired}",
                    SelectedByDefault = true,
                    ProposedField = proposed
                });
            }
        }

        foreach (var proposed in draft.Fields.Where(f => ControlType.IsMultiSelect(f.ControlType)))
        {
            if (formFields.ContainsKey(proposed.PropertyName))
                continue;
            if (entityProps.Any(p => p.Name.Equals(proposed.PropertyName, StringComparison.OrdinalIgnoreCase)))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"field:{proposed.PropertyName}",
                ChangeType = FormSchemaSyncChangeTypes.Add,
                Target = FormSchemaSyncTargets.Field,
                Name = proposed.PropertyName,
                Description = $"Add MultiSelect junction field '{proposed.PropertyName}'.",
                ProposedSummary = $"Control={proposed.ControlType}, Lookup={proposed.LookupEntity}, Mapping={proposed.MappingEntity}",
                SelectedByDefault = true,
                ProposedField = proposed
            });
        }

        foreach (var field in form.Fields)
        {
            if (entityProps.Any(p => p.Name.Equals(field.PropertyName, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (ControlType.IsMultiSelect(field.ControlType))
                continue;

            if (string.Equals(field.ControlType, ControlType.Hidden, StringComparison.OrdinalIgnoreCase)
                && field.PropertyName.EndsWith("Id", StringComparison.Ordinal))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"field:{field.PropertyName}",
                ChangeType = FormSchemaSyncChangeTypes.Remove,
                Target = FormSchemaSyncTargets.Field,
                Name = field.PropertyName,
                Description = $"Property '{field.PropertyName}' no longer exists on the entity.",
                CurrentSummary = $"Control={field.ControlType}, Label={field.Label}",
                SelectedByDefault = false
            });
        }

        return changes;
    }

    private static IEnumerable<FormSchemaSyncChangeDto> BuildGridColumnChanges(
        FormConfigDto form,
        IReadOnlyList<EntityPropertyMetadataDto> entityProps,
        HashSet<string> entityPropNames,
        FormConfigDto draft)
    {
        var changes = new List<FormSchemaSyncChangeDto>();
        var isDetailForm = form.FormType.Equals(FormType.Detail.ToString(), StringComparison.OrdinalIgnoreCase);

        if (isDetailForm)
            return changes;

        var formColumns = form.GridColumns.ToDictionary(c => c.PropertyName, StringComparer.OrdinalIgnoreCase);
        var draftColumns = draft.GridColumns.ToDictionary(c => c.PropertyName, StringComparer.OrdinalIgnoreCase);

        foreach (var column in draft.GridColumns)
        {
            if (formColumns.ContainsKey(column.PropertyName))
                continue;

            if (!entityPropNames.Contains(column.PropertyName))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"column:{column.PropertyName}",
                ChangeType = FormSchemaSyncChangeTypes.Add,
                Target = FormSchemaSyncTargets.GridColumn,
                Name = column.PropertyName,
                Description = $"Add list grid column for '{column.PropertyName}'.",
                ProposedSummary = column.Label,
                SelectedByDefault = true,
                ProposedColumn = column
            });
        }

        foreach (var column in form.GridColumns)
        {
            if (entityPropNames.Contains(column.PropertyName))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"column:{column.PropertyName}",
                ChangeType = FormSchemaSyncChangeTypes.Remove,
                Target = FormSchemaSyncTargets.GridColumn,
                Name = column.PropertyName,
                Description = $"Remove grid column '{column.PropertyName}' (property removed from entity).",
                CurrentSummary = column.Label,
                SelectedByDefault = false
            });
        }

        return changes;
    }

    private static IEnumerable<FormSchemaSyncChangeDto> BuildRelationChanges(
        FormConfigDto form,
        EntityMetadataDto metadata,
        FormConfigDto draft)
    {
        var changes = new List<FormSchemaSyncChangeDto>();

        var formRelations = form.Relations.ToDictionary(RelationKey, StringComparer.OrdinalIgnoreCase);
        var draftRelations = draft.Relations.ToDictionary(RelationKey, StringComparer.OrdinalIgnoreCase);

        foreach (var relation in draft.Relations)
        {
            var key = RelationKey(relation);
            if (formRelations.ContainsKey(key))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"relation:{key}",
                ChangeType = FormSchemaSyncChangeTypes.Add,
                Target = FormSchemaSyncTargets.Relation,
                Name = key,
                Description = $"Add relation {relation.ParentEntity} → {relation.ChildEntity}.",
                ProposedSummary = $"{relation.RelationType}, FK={relation.ForeignKey}",
                SelectedByDefault = true,
                ProposedRelation = relation
            });
        }

        foreach (var relation in form.Relations)
        {
            var key = RelationKey(relation);
            if (draftRelations.ContainsKey(key))
                continue;

            changes.Add(new FormSchemaSyncChangeDto
            {
                Key = $"relation:{key}",
                ChangeType = FormSchemaSyncChangeTypes.Remove,
                Target = FormSchemaSyncTargets.Relation,
                Name = key,
                Description = $"Remove relation {relation.ParentEntity} → {relation.ChildEntity} (no longer on entity).",
                CurrentSummary = $"{relation.RelationType}, FK={relation.ForeignKey}",
                SelectedByDefault = false
            });
        }

        return changes;
    }

    private static void ApplyFieldChange(FormConfigDto form, FormSchemaSyncChangeDto change)
    {
        switch (change.ChangeType)
        {
            case FormSchemaSyncChangeTypes.Add:
                if (change.ProposedField == null) return;
                form.Fields.Add(CloneField(change.ProposedField, form.Fields.Count));
                break;

            case FormSchemaSyncChangeTypes.Remove:
                form.Fields.RemoveAll(f => f.PropertyName.Equals(change.Name, StringComparison.OrdinalIgnoreCase));
                break;

            case FormSchemaSyncChangeTypes.Update:
                var field = form.Fields.FirstOrDefault(f => f.PropertyName.Equals(change.Name, StringComparison.OrdinalIgnoreCase));
                if (field == null) return;
                if (!string.IsNullOrWhiteSpace(change.ProposedControlType))
                    field.ControlType = change.ProposedControlType;
                if (change.ProposedIsRequired.HasValue)
                    field.IsRequired = change.ProposedIsRequired.Value;
                break;
        }
    }

    private static void ApplyGridColumnChange(FormConfigDto form, FormSchemaSyncChangeDto change)
    {
        switch (change.ChangeType)
        {
            case FormSchemaSyncChangeTypes.Add:
                if (change.ProposedColumn == null) return;
                form.GridColumns.Add(CloneColumn(change.ProposedColumn, form.GridColumns.Count));
                break;

            case FormSchemaSyncChangeTypes.Remove:
                form.GridColumns.RemoveAll(c => c.PropertyName.Equals(change.Name, StringComparison.OrdinalIgnoreCase));
                break;
        }
    }

    private static void ApplyRelationChange(FormConfigDto form, FormSchemaSyncChangeDto change)
    {
        switch (change.ChangeType)
        {
            case FormSchemaSyncChangeTypes.Add:
                if (change.ProposedRelation == null) return;
                form.Relations.Add(CloneRelation(change.ProposedRelation, form.Relations.Count));
                break;

            case FormSchemaSyncChangeTypes.Remove:
                form.Relations.RemoveAll(r => RelationKey(r).Equals(change.Name, StringComparison.OrdinalIgnoreCase));
                break;
        }
    }

    private static List<EntityPropertyMetadataDto> GetFormProperties(EntityMetadataDto metadata) =>
        metadata.Properties
            .Where(p => !p.IsKey && !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static string RelationKey(FormRelationConfigDto relation) =>
        $"{relation.RelationType}:{relation.ChildEntity}:{relation.ForeignKey}";

    public static string PrefixKey(string entityName, string key) =>
        $"{entityName}|{key}";

    public static bool TryParsePrefixedKey(string prefixedKey, out string entityName, out string localKey)
    {
        var separatorIndex = prefixedKey.IndexOf('|');
        if (separatorIndex <= 0)
        {
            entityName = string.Empty;
            localKey = prefixedKey;
            return false;
        }

        entityName = prefixedKey[..separatorIndex];
        localKey = prefixedKey[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(entityName) && !string.IsNullOrWhiteSpace(localKey);
    }

    public static void PrefixChanges(IEnumerable<FormSchemaSyncChangeDto> changes, string entityName)
    {
        foreach (var change in changes)
            change.Key = PrefixKey(entityName, change.Key);
    }

    private static FormConfigDto CloneForm(FormConfigDto form) => new()
    {
        Id = form.Id,
        Code = form.Code,
        Name = form.Name,
        EntityName = form.EntityName,
        TableName = form.TableName,
        GroupName = form.GroupName,
        FormType = form.FormType,
        DisplayOrder = form.DisplayOrder,
        IsActive = form.IsActive,
        Fields = form.Fields.Select(f => CloneField(f, f.DisplayOrder)).ToList(),
        GridColumns = form.GridColumns.Select(c => CloneColumn(c, c.DisplayOrder)).ToList(),
        GridActions = form.GridActions.Select(a => new FormGridActionConfigDto
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
        Relations = form.Relations.Select(r => CloneRelation(r, r.DisplayOrder)).ToList()
    };

    private static FormFieldConfigDto CloneField(FormFieldConfigDto field, int order) => new()
    {
        Id = field.Id,
        PropertyName = field.PropertyName,
        Label = field.Label,
        ControlType = field.ControlType,
        IsRequired = field.IsRequired,
        IsVisible = field.IsVisible,
        IsReadOnly = field.IsReadOnly,
        DisplayOrder = order,
        ValidationRule = field.ValidationRule,
        ConditionalRule = field.ConditionalRule,
        LookupEntity = field.LookupEntity,
        LookupParentField = field.LookupParentField,
        LookupFilterField = field.LookupFilterField,
        MappingEntity = field.MappingEntity,
        MappingParentKey = field.MappingParentKey,
        MappingRelatedKey = field.MappingRelatedKey,
        SectionName = field.SectionName
    };

    private static FormGridColumnConfigDto CloneColumn(FormGridColumnConfigDto column, int order) => new()
    {
        Id = column.Id,
        PropertyName = column.PropertyName,
        Label = column.Label,
        DisplayOrder = order,
        IsSortable = column.IsSortable,
        IsSearchable = column.IsSearchable,
        IsVisible = column.IsVisible,
        DisplayFormat = column.DisplayFormat
    };

    private static FormRelationConfigDto CloneRelation(FormRelationConfigDto relation, int order) => new()
    {
        Id = relation.Id,
        RelationType = relation.RelationType,
        ParentEntity = relation.ParentEntity,
        ChildEntity = relation.ChildEntity,
        ForeignKey = relation.ForeignKey,
        NavigationProperty = relation.NavigationProperty,
        TabLabel = relation.TabLabel,
        DisplayOrder = order
    };

    private static void ReorderFields(FormConfigDto form)
    {
        for (var i = 0; i < form.Fields.Count; i++)
            form.Fields[i].DisplayOrder = i;
    }

    private static void ReorderGridColumns(FormConfigDto form)
    {
        for (var i = 0; i < form.GridColumns.Count; i++)
            form.GridColumns[i].DisplayOrder = i;
    }

    private static void ReorderRelations(FormConfigDto form)
    {
        for (var i = 0; i < form.Relations.Count; i++)
            form.Relations[i].DisplayOrder = i;
    }
}
