namespace MetaForge.Application.Interfaces;

/// <summary>
/// Audit trail recording service used by application code (e.g. CRUD operations).
/// </summary>
public interface IAuditService
{
    Task LogAsync(string entityName, string recordId, string action, string? oldValue, string? newValue, CancellationToken cancellationToken = default);
}
