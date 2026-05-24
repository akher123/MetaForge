using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Claims;
using MetaForge.Infrastructure.Dynamic;
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
            data.TryGetValue(field.PropertyName, out var value);
            var strValue = DynamicEntityMapper.ToStringValue(value);
            var isLookupField = !string.IsNullOrWhiteSpace(field.LookupEntity)
                || (field.PropertyName.EndsWith("Id", StringComparison.Ordinal)
                    && !field.PropertyName.Equals("Id", StringComparison.OrdinalIgnoreCase));

            if (field.IsRequired && isLookupField)
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
            else if (field.IsRequired && string.IsNullOrWhiteSpace(strValue))
            {
                failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} is required."));
                continue;
            }
            else if (isLookupField && !string.IsNullOrWhiteSpace(field.LookupEntity))
            {
                var lookupId = DynamicEntityMapper.ToInt32(value);
                if (lookupId > 0 && !await ForeignKeyExistsAsync(field.LookupEntity, lookupId, cancellationToken))
                    failures.Add(new ValidationFailure(field.PropertyName, $"{field.Label} reference is invalid."));
            }

            if (string.IsNullOrWhiteSpace(field.ValidationRule) || string.IsNullOrWhiteSpace(strValue))
                continue;

            ApplyRule(field.PropertyName, field.Label, field.ValidationRule, strValue, failures);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
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

    private static void ApplyRule(string property, string label, string rule, string value, List<ValidationFailure> failures)
    {
        var parts = rule.Split(':', 2, StringSplitOptions.TrimEntries);
        var ruleName = parts[0];
        var ruleValue = parts.Length > 1 ? parts[1] : null;

        switch (ruleName.ToLowerInvariant())
        {
            case "maxlength" when int.TryParse(ruleValue, out var maxLen) && value.Length > maxLen:
                failures.Add(new ValidationFailure(property, $"{label} must not exceed {maxLen} characters."));
                break;
            case "minlength" when int.TryParse(ruleValue, out var minLen) && value.Length < minLen:
                failures.Add(new ValidationFailure(property, $"{label} must be at least {minLen} characters."));
                break;
            case "range" when ruleValue != null:
                var rangeParts = ruleValue.Split('-');
                if (rangeParts.Length == 2 && decimal.TryParse(value, out var num)
                    && decimal.TryParse(rangeParts[0], out var min)
                    && decimal.TryParse(rangeParts[1], out var max)
                    && (num < min || num > max))
                {
                    failures.Add(new ValidationFailure(property, $"{label} must be between {min} and {max}."));
                }
                break;
            case "regex" when ruleValue != null && !Regex.IsMatch(value, ruleValue):
                failures.Add(new ValidationFailure(property, $"{label} format is invalid."));
                break;
            case "email" when !value.Contains('@'):
                failures.Add(new ValidationFailure(property, $"{label} must be a valid email."));
                break;
        }
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

    public FormAuthorizationService(MetaForgeDbContext dbContext) => _dbContext = dbContext;

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

    public Task<bool> HasFormPermissionAsync(ClaimsPrincipal user, string formCode, string action, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        if (user.IsInRole("Administrator"))
            return Task.FromResult(true);

        var code = $"{formCode}.{action}";
        var allowed = user.Claims.Any(c =>
            c.Type == Shared.Constants.AppConstants.PermissionClaimType
            && string.Equals(c.Value, code, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(allowed);
    }

    public Task<bool> HasPermissionCodeAsync(ClaimsPrincipal user, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        if (user.IsInRole("Administrator"))
            return Task.FromResult(true);

        return Task.FromResult(user.Claims.Any(c =>
            c.Type == Shared.Constants.AppConstants.PermissionClaimType
            && string.Equals(c.Value, permissionCode, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<FormPermissionsDto> GetFormPermissionsAsync(ClaimsPrincipal user, string formCode, CancellationToken cancellationToken = default)
    {
        var isAdmin = user.IsInRole("Administrator");
        bool Has(string action) => isAdmin || user.Claims.Any(c =>
            c.Type == Shared.Constants.AppConstants.PermissionClaimType
            && string.Equals(c.Value, $"{formCode}.{action}", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(new FormPermissionsDto
        {
            FormCode = formCode,
            CanView = Has(PermissionAction.View),
            CanCreate = Has(PermissionAction.Create),
            CanEdit = Has(PermissionAction.Edit),
            CanDelete = Has(PermissionAction.Delete),
            CanExport = Has(PermissionAction.Export),
            CanApprove = Has(PermissionAction.Approve)
        });
    }

    public async Task<string?> ResolveFormCodeByEntityAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var form = await _dbContext.ForgeForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.EntityName == entityName, cancellationToken);

        return form?.Code;
    }
}
