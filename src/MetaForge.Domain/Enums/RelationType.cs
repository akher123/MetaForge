namespace MetaForge.Domain.Enums;

/// <summary>
/// Supported entity relationship types.
/// </summary>
public static class RelationType
{
    public const string OneToOne = "OneToOne";
    public const string OneToMany = "OneToMany";
    public const string ManyToOne = "ManyToOne";

    public static readonly IReadOnlyList<string> All =
        [OneToOne, OneToMany, ManyToOne];
}
