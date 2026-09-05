using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Triggers email sends from feature lifecycle events based on template bindings.
/// </summary>
public class EmailTriggerService : IEmailTriggerService
{
    private readonly MetaForgeDbContext _db;
    private readonly IEmailDispatchService _dispatchService;
    private readonly IEntityTypeResolver _typeResolver;

    public EmailTriggerService(
        MetaForgeDbContext db,
        IEmailDispatchService dispatchService,
        IEntityTypeResolver typeResolver)
    {
        _db = db;
        _dispatchService = dispatchService;
        _typeResolver = typeResolver;
    }

    public async Task TriggerAsync(
        string entityName,
        int recordId,
        string triggerEvent,
        string? actionCode = null,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.ForgeForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.EntityName == entityName && f.IsActive, cancellationToken);

        if (form == null)
            return;

        var bindings = await _db.EmailTemplateBindings
            .Include(b => b.EmailTemplate)
            .Where(b => b.IsActive
                && b.FormId == form.Id
                && b.TriggerEvent == triggerEvent
                && b.EmailTemplate.IsActive)
            .ToListAsync(cancellationToken);

        if (bindings.Count == 0)
            return;

        var record = await LoadRecordAsync(entityName, recordId, cancellationToken);

        foreach (var binding in bindings)
        {
            if (triggerEvent == EmailTriggerEvent.OnAction
                && !string.Equals(binding.ActionCode, actionCode, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!EvaluateCondition(binding.ConditionExpression, record))
                continue;

            var toAddress = ResolveRecipient(binding, record);
            await _dispatchService.EnqueueFromTemplateAsync(new EmailSendRequest
            {
                TemplateCode = binding.EmailTemplate.Code,
                EntityName = entityName,
                RecordId = recordId,
                ToAddress = toAddress
            }, cancellationToken);
        }
    }

    private static bool EvaluateCondition(string? expression, Dictionary<string, object?> record)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        // Simple equality: FieldName=Value
        var parts = expression.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return true;

        if (!record.TryGetValue(parts[0], out var value))
            return false;

        return string.Equals(value?.ToString(), parts[1], StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveRecipient(EmailTemplateBinding binding, Dictionary<string, object?> record)
    {
        if (!string.IsNullOrWhiteSpace(binding.RecipientField)
            && record.TryGetValue(binding.RecipientField, out var value)
            && value != null)
            return value.ToString();

        return null;
    }

    private async Task<Dictionary<string, object?>> LoadRecordAsync(
        string entityName,
        int recordId,
        CancellationToken cancellationToken)
    {
        var entityType = _typeResolver.Resolve(entityName);
        var entity = await _db.FindAsync(entityType, [recordId], cancellationToken)
            ?? throw new NotFoundException($"{entityName} with id {recordId} was not found.");

        return DynamicEntityMapper.ToDictionary(entity);
    }
}
