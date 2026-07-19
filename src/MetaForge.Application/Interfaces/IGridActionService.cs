namespace MetaForge.Application.Interfaces;

/// <summary>
/// Executes configured grid actions with permission checks.
/// </summary>
public interface IGridActionService
{
    Task ExecuteAsync(
        string formCode,
        string actionCode,
        string? recordId,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
