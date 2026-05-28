using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Executes metadata-configured API grid actions on behalf of the current user.
/// </summary>
public class GridActionService : IGridActionService
{
    private static readonly Regex CrudPathPattern = new(
        @"^/api/metaforge/crud/(?<entity>[^/]+)(?:/(?<id>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IFormMetadataCache _formCache;
    private readonly IGenericCrudService _crudService;
    private readonly IFormAuthorizationService _authorizationService;

    public GridActionService(
        IFormMetadataCache formCache,
        IGenericCrudService crudService,
        IFormAuthorizationService authorizationService)
    {
        _formCache = formCache;
        _crudService = crudService;
        _authorizationService = authorizationService;
    }

    public async Task ExecuteAsync(
        string formCode,
        string actionCode,
        int? recordId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        var action = form.GridActions
            .FirstOrDefault(a => a.IsActive && string.Equals(a.Code, actionCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"Grid action '{actionCode}' was not found on form '{formCode}'.");

        if (!string.Equals(action.HandlerType, GridActionHandlerType.Api, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Only API grid actions can be executed on the server.");

        var permission = string.IsNullOrWhiteSpace(action.PermissionAction)
            ? PermissionAction.View
            : action.PermissionAction;

        if (!await _authorizationService.HasFormPermissionAsync(user, formCode, permission, cancellationToken))
            throw new BusinessException("You do not have permission to run this action.");

        var context = await BuildContextAsync(form, recordId, cancellationToken);
        var target = ResolveTemplate(action.HandlerTarget, context);

        if (!CrudPathPattern.IsMatch(target))
            throw new BusinessException("Grid action target must point to /api/metaforge/crud/{entity}/{id}.");

        var match = CrudPathPattern.Match(target);
        var entity = match.Groups["entity"].Value;
        var idGroup = match.Groups["id"];
        var method = action.HttpMethod.Trim().ToUpperInvariant();

        if (method == "GET")
        {
            if (!idGroup.Success)
                throw new BusinessException("GET grid actions require a record id in the target URL.");

            await _crudService.GetByIdAsync(entity, int.Parse(idGroup.Value), cancellationToken);
            return;
        }

        if (method == "DELETE")
        {
            if (!idGroup.Success)
                throw new BusinessException("DELETE grid actions require a record id in the target URL.");

            await _crudService.DeleteAsync(entity, int.Parse(idGroup.Value), cancellationToken);
            return;
        }

        if (method is "POST" or "PUT" or "PATCH")
        {
            var payload = ParseRequestBody(action.RequestBody, context);

            if (method == "POST")
            {
                await _crudService.CreateAsync(entity, payload, cancellationToken);
                return;
            }

            if (!idGroup.Success)
                throw new BusinessException("Update grid actions require a record id in the target URL.");

            var targetRecordId = int.Parse(idGroup.Value);
            var existing = await _crudService.GetByIdAsync(entity, targetRecordId, cancellationToken);
            foreach (var (key, value) in existing)
            {
                if (!payload.ContainsKey(key))
                    payload[key] = value;
            }

            await _crudService.UpdateAsync(entity, targetRecordId, payload, cancellationToken);
            return;
        }

        throw new BusinessException($"HTTP method '{action.HttpMethod}' is not supported for grid actions.");
    }

    private async Task<Dictionary<string, string?>> BuildContextAsync(
        ForgeForm form,
        int? recordId,
        CancellationToken cancellationToken)
    {
        var context = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formCode"] = form.Code,
            ["entity"] = form.EntityName,
            ["id"] = recordId?.ToString()
        };

        if (!recordId.HasValue)
            return context;

        var record = await _crudService.GetByIdAsync(form.EntityName, recordId.Value, cancellationToken);
        foreach (var (key, value) in record)
        {
            context[key] = value?.ToString();
        }

        return context;
    }

    private static string ResolveTemplate(string template, IReadOnlyDictionary<string, string?> context)
    {
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            return context.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        });
    }

    private static Dictionary<string, object?> ParseRequestBody(string? requestBody, IReadOnlyDictionary<string, string?> context)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return [];

        var resolved = ResolveTemplate(requestBody, context);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(resolved)
            ?? throw new BusinessException("Grid action request body is not valid JSON.");
    }
}
