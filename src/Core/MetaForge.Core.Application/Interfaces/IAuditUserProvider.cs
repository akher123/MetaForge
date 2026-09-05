namespace MetaForge.Application.Interfaces;

/// <summary>
/// Resolves the current user name for audit attribution.
/// </summary>
public interface IAuditUserProvider
{
    string GetCurrentUserName();
}
