namespace MetaForge.Application.Interfaces;

/// <summary>
/// Dynamic FluentValidation from metadata rules.
/// </summary>
public interface IDynamicValidationService
{
    Task ValidateAsync(string entityName, Dictionary<string, object?> data, CancellationToken cancellationToken = default);
}
