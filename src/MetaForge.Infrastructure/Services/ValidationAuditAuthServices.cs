using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Security.Claims;
using MetaForge.Domain.Enums;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Validation;
using MetaForge.Shared.Constants;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Generates FluentValidation rules at runtime from field metadata.
/// </summary>
public class DynamicValidationService : IDynamicValidationService
{
    private readonly IFormMetadataCache _formCache;
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;

    public DynamicValidationService(
        IFormMetadataCache formCache,
        MetaForgeDbContext dbContext,
        IEntityTypeResolver typeResolver)
    {
        _formCache = formCache;
        _dbContext = dbContext;
        _typeResolver = typeResolver;
    }

    public async Task ValidateAsync(string entityName, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        if (form == null) return;

        var failures = new List<ValidationFailure>();

        foreach (var field in form.Fields)
        {
            var effective = FieldConditionalRuleEngine.EvaluateEffectiveState(field, data);
            if (!effective.IsVisible)
                continue;

            data.TryGetValue(field.PropertyName, out var value);

            if (MappingAssociationService.IsMultiSelectField(field))
            {
                await ValidateMultiSelectFieldAsync(field, value, data, effective.IsRequired, failures, cancellationToken);
                continue;
            }

            var strValue = DynamicEntityMapper.ToStringValue(value);
            var isSingleLookupField = ControlType.IsSingleLookup(field.ControlType)
                || (!ControlType.IsMultiSelect(field.ControlType)
                    && (field.PropertyName.EndsWith("Id", StringComparison.Ordinal)
                        && !field.PropertyName.Equals("Id", StringComparison.OrdinalIgnoreCase)));

            if (effective.IsRequired && isSingleLookupField)
            {
                var lookupId = DynamicEntityMapper.ToInt32(value);
                if (lookupId <= 0)
                {
                    failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} is required."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(field.LookupEntity)
                    && !await ForeignKeyExistsAsync(field.LookupEntity, lookupId, cancellationToken))
                {
                    failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} reference is invalid."));
                    continue;
                }
            }
            else if (effective.IsRequired && string.IsNullOrWhiteSpace(strValue))
            {
                failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} is required."));
                continue;
            }
            else if (isSingleLookupField && !string.IsNullOrWhiteSpace(field.LookupEntity))
            {
                var lookupId = DynamicEntityMapper.ToInt32(value);
                if (lookupId > 0 && !await ForeignKeyExistsAsync(field.LookupEntity, lookupId, cancellationToken))
                    failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} reference is invalid."));
            }

            if (string.IsNullOrWhiteSpace(field.ValidationRule))
                continue;

            FieldValidationRuleEngine.ApplyRules(field.PropertyName, field.Label, field.ValidationRule, data, failures);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private async Task ValidateMultiSelectFieldAsync(
        Domain.Metadata.ForgeField field,
        object? value,
        Dictionary<string, object?> data,
        bool isRequired,
        List<ValidationFailure> failures,
        CancellationToken cancellationToken)
    {
        var ids = DynamicEntityMapper.ToInt32List(value);

        if (isRequired && ids.Count == 0)
        {
            failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(field.LookupEntity))
            return;

        foreach (var lookupId in ids)
        {
            if (!await ForeignKeyExistsAsync(field.LookupEntity, lookupId, cancellationToken))
            {
                failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} contains an invalid reference."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(field.LookupParentField)
                && !await LookupMatchesCascadeFilterAsync(field, lookupId, data, cancellationToken))
            {
                failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} contains a value that does not match the parent selection."));
                return;
            }
        }
    }

    private async Task<bool> LookupMatchesCascadeFilterAsync(
        Domain.Metadata.ForgeField field,
        int lookupId,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(field.LookupParentField))
            return true;

        if (!data.TryGetValue(field.LookupParentField, out var parentValue))
            return true;

        var parentFilterValue = DynamicEntityMapper.ToInt32(parentValue);
        if (parentFilterValue <= 0)
            return true;

        var lookupEntity = _typeResolver.Resolve(field.LookupEntity!);
        var filterPropertyName = string.IsNullOrWhiteSpace(field.LookupFilterField)
            ? field.LookupParentField
            : field.LookupFilterField;

        var entity = await _dbContext.FindAsync(lookupEntity, [lookupId], cancellationToken);
        if (entity == null)
            return false;

        var filterProperty = lookupEntity.GetProperty(filterPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (filterProperty == null)
            return true;

        var actual = DynamicEntityMapper.ToInt32(filterProperty.GetValue(entity));
        return actual == parentFilterValue;
    }

    private async Task<bool> ForeignKeyExistsAsync(string lookupEntity, int id, CancellationToken cancellationToken)
    {
        var entityType = _typeResolver.Resolve(lookupEntity);
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType);
        var dbSet = setMethod.Invoke(_dbContext, null)!;

        var parameter = Expression.Parameter(entityType, "e");
        var idProperty = Expression.Property(parameter, "Id");
        var constant = Expression.Constant(id);
        var equality = Expression.Equal(idProperty, Expression.Convert(constant, idProperty.Type));
        var lambda = Expression.Lambda(equality, parameter);

        var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync)
                        && m.GetParameters().Length == 3)
            .MakeGenericMethod(entityType);

        var query = (IQueryable)dbSet;
        var task = (Task<bool>)anyAsyncMethod.Invoke(null, [query, lambda, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }

}

/// <summary>
/// Audit trail persistence service.
/// </summary>
public class AuditService : IAuditService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(MetaForgeDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string entityName, string recordId, string action, string? oldValue, string? newValue, CancellationToken cancellationToken = default)
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";

        _dbContext.AuditLogs.Add(new Domain.Audit.AuditLog
        {
            EntityName = entityName,
            RecordId = recordId,
            Action = action,
            UserName = userName,
            Timestamp = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Role-based module authorization.
/// </summary>
public class FormAuthorizationService : IFormAuthorizationService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IUserAuthorizationSnapshotProvider _snapshotProvider;

    public FormAuthorizationService(
        MetaForgeDbContext dbContext,
        IEntityTypeResolver typeResolver,
        IUserAuthorizationSnapshotProvider snapshotProvider)
    {
        _dbContext = dbContext;
        _typeResolver = typeResolver;
        _snapshotProvider = snapshotProvider;
    }

    public async Task<bool> HasPermissionAsync(int userId, string formCode, string action, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        return permissions.Contains($"{formCode}.{action}", StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasFormPermissionAsync(ClaimsPrincipal user, string formCode, string action, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(user, cancellationToken);
        return snapshot?.HasPermission($"{formCode}.{action}") == true;
    }

    public async Task<bool> HasPermissionCodeAsync(ClaimsPrincipal user, string permissionCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(user, cancellationToken);
        return snapshot?.HasPermission(permissionCode) == true;
    }

    public async Task<FormPermissionsDto> GetFormPermissionsAsync(ClaimsPrincipal user, string formCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(user, cancellationToken);
        var prefix = $"{formCode}.";

        HashSet<string> granted;
        if (snapshot?.IsAdministrator == true)
        {
            granted = new HashSet<string>(PermissionAction.All, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            granted = snapshot?.Permissions
                .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(p => p[prefix.Length..])
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new FormPermissionsDto
        {
            FormCode = formCode,
            CanView = granted.Contains(PermissionAction.View),
            CanCreate = granted.Contains(PermissionAction.Create),
            CanEdit = granted.Contains(PermissionAction.Edit),
            CanDelete = granted.Contains(PermissionAction.Delete),
            CanExport = granted.Contains(PermissionAction.Export),
            CanApprove = granted.Contains(PermissionAction.Approve),
            GrantedActions = granted
        };
    }

    public async Task<string?> ResolveFormCodeByEntityAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var form = await _dbContext.ForgeForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.EntityName == entityName, cancellationToken);

        return form?.Code;
    }

    public async Task<bool> CanAccessLookupAsync(ClaimsPrincipal user, string entityName, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (!_typeResolver.IsBusinessEntity(entityName))
            return false;

        var snapshot = await _snapshotProvider.GetSnapshotAsync(user, cancellationToken);
        if (snapshot == null)
            return false;

        if (snapshot.IsAdministrator)
            return true;

        if (snapshot.HasPermission(ConfigPermissions.View) || snapshot.HasPermission(ConfigPermissions.Manage))
            return true;

        var formCodes = await GetLookupReferencingFormCodesAsync(entityName, cancellationToken);
        foreach (var formCode in formCodes)
        {
            if (await HasFormPermissionAsync(user, formCode, PermissionAction.View, cancellationToken))
                return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> GetLookupReferencingFormCodesAsync(
        string lookupEntityName,
        CancellationToken cancellationToken)
    {
        var forms = await _dbContext.ForgeForms
            .AsNoTracking()
            .Where(f => f.IsActive)
            .Select(f => new
            {
                f.Code,
                f.EntityName,
                f.FormType,
                FieldLookups = f.Fields.Select(field => field.LookupEntity),
                ChildEntities = f.Relations.Select(relation => relation.ChildEntity)
            })
            .ToListAsync(cancellationToken);

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var form in forms)
        {
            if (string.Equals(form.EntityName, lookupEntityName, StringComparison.OrdinalIgnoreCase)
                || form.FieldLookups.Any(l => string.Equals(l, lookupEntityName, StringComparison.OrdinalIgnoreCase)))
            {
                codes.Add(form.Code);
            }
        }

        var detailEntityByCode = forms
            .Where(f => f.FormType == FormType.Detail)
            .ToDictionary(f => f.Code, f => f.EntityName, StringComparer.OrdinalIgnoreCase);

        foreach (var formCode in codes.ToList())
        {
            if (!detailEntityByCode.TryGetValue(formCode, out var detailEntity))
                continue;

            foreach (var parent in forms)
            {
                if (parent.ChildEntities.Any(child =>
                        string.Equals(child, detailEntity, StringComparison.OrdinalIgnoreCase)))
                {
                    codes.Add(parent.Code);
                }
            }
        }

        return codes.ToList();
    }
}
