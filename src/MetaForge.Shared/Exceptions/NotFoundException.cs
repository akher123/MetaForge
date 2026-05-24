namespace MetaForge.Shared.Exceptions;

/// <summary>
/// Exception when a requested entity is not found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
