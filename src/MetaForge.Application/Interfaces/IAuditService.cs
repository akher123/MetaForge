namespace MetaForge.Application.Interfaces;

/// <summary>
/// Audit trail recording service.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string entityName, string recordId, string action, string? oldValue, string? newValue, CancellationToken cancellationToken = default);
}
