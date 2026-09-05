using MetaForge.Application.DTOs;
using MetaForge.Domain.Enums;
using MetaForge.Infrastructure.Dynamic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Infers MultiSelect form fields and junction-table mapping settings from EF Core metadata.
/// </summary>
public static class MultiSelectFieldInference
{
    public static bool TryParseRelatedEntityName(string propertyName, out string relatedEntityName)
    {
        relatedEntityName = string.Empty;
        if (string.IsNullOrWhiteSpace(propertyName) || !propertyName.EndsWith("Ids", StringComparison.Ordinal))
            return false;

        relatedEntityName = propertyName[..^3];
        return relatedEntityName.Length > 0;
    }

    public static IReadOnlyList<FormFieldConfigDto> DiscoverJunctionFields(
        MetaForgeDbContext dbContext,
        EntityMetadataDto masterMetadata)
    {
        var results = new List<FormFieldConfigDto>();
        var masterEntityName = masterMetadata.EntityName;

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var junction = TryMapJunction(dbContext, entityType, masterEntityName, masterMetadata);
            if (junction == null)
                continue;

            results.Add(junction);
        }

        return results;
    }

    public static void ApplyDefaults(FormFieldConfigDto field, EntityMetadataDto masterMetadata, MetaForgeDbContext? dbContext = null)
    {
        if (!ControlType.IsMultiSelect(field.ControlType))
            return;

        if (TryParseRelatedEntityName(field.PropertyName, out var relatedEntity))
        {
            field.LookupEntity ??= relatedEntity;
            field.MappingEntity ??= masterMetadata.EntityName + relatedEntity;
            field.MappingParentKey ??= masterMetadata.EntityName + "Id";
            field.MappingRelatedKey ??= relatedEntity + "Id";
        }

        field.LookupParentField ??= InferCascadeParentField(dbContext, masterMetadata, field.LookupEntity);
        field.LookupValueField ??= LookupFieldResolver.DefaultValueField;
        field.LookupTextField ??= string.IsNullOrWhiteSpace(field.LookupEntity)
            ? null
            : LookupFieldResolver.DefaultTextField;
    }

    private static FormFieldConfigDto? TryMapJunction(
        MetaForgeDbContext dbContext,
        IEntityType entityType,
        string masterEntityName,
        EntityMetadataDto masterMetadata)
    {
        var pk = entityType.FindPrimaryKey();
        if (pk == null || pk.Properties.Count != 2)
            return null;

        var foreignKeys = entityType.GetForeignKeys().ToList();
        if (foreignKeys.Count < 2)
            return null;

        var masterFk = foreignKeys.FirstOrDefault(fk =>
            string.Equals(fk.PrincipalEntityType.ClrType.Name, masterEntityName, StringComparison.OrdinalIgnoreCase));
        if (masterFk == null)
            return null;

        var relatedFk = foreignKeys.FirstOrDefault(fk => fk != masterFk);
        if (relatedFk == null)
            return null;

        var relatedEntityName = relatedFk.PrincipalEntityType.ClrType.Name;
        var propertyName = relatedEntityName + "Ids";

        return new FormFieldConfigDto
        {
            PropertyName = propertyName,
            Label = SplitPascalCase(relatedEntityName),
            ControlType = ControlType.MultiSelect,
            IsRequired = false,
            IsVisible = true,
            LookupEntity = relatedEntityName,
            LookupParentField = InferCascadeParentField(dbContext, masterMetadata, relatedEntityName),
            LookupValueField = LookupFieldResolver.DefaultValueField,
            LookupTextField = LookupFieldResolver.DefaultTextField,
            MappingEntity = entityType.ClrType.Name,
            MappingParentKey = masterFk.Properties.First().Name,
            MappingRelatedKey = relatedFk.Properties.First().Name
        };
    }

    private static string? InferCascadeParentField(
        MetaForgeDbContext? dbContext,
        EntityMetadataDto masterMetadata,
        string? lookupEntity)
    {
        if (string.IsNullOrWhiteSpace(lookupEntity))
            return null;

        var masterFkNames = masterMetadata.Properties
            .Where(p => p.IsForeignKey)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (dbContext != null)
        {
            var lookupType = dbContext.Model.GetEntityTypes()
                .FirstOrDefault(t => string.Equals(t.ClrType.Name, lookupEntity, StringComparison.OrdinalIgnoreCase));

            if (lookupType != null)
            {
                foreach (var fk in lookupType.GetForeignKeys())
                {
                    var fkName = fk.Properties.First().Name;
                    if (masterFkNames.Contains(fkName))
                        return fkName;
                }
            }
        }

        return masterMetadata.Relations
            .Where(r => string.Equals(r.ParentEntity, lookupEntity, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ForeignKey)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) && masterFkNames.Contains(name));
    }

    private static string SplitPascalCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
