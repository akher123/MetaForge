namespace MetaForge.Shared.Exceptions;

/// <summary>
/// Exception for business rule violations.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }

    public BusinessException(string message, Exception innerException)
        : base(message, innerException) { }
}
