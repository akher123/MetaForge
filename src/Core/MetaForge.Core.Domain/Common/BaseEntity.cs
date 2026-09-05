namespace MetaForge.Domain.Common;

/// <summary>
/// Base entity with a typed primary key.
/// </summary>
/// <typeparam name="TKey">Primary key CLR type (e.g. <see cref="int"/>, <see cref="long"/>, <see cref="Guid"/>).</typeparam>
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
}

/// <summary>
/// Base entity with integer primary key (default).
/// </summary>
public abstract class BaseEntity : BaseEntity<int>
{
}
