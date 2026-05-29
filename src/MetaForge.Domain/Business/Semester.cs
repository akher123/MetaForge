namespace MetaForge.Domain.Business;

/// <summary>
/// Semester business entity (scaffolded from Semesters).
/// </summary>
public class Semester : BaseEntity
{
    public string SemesterName { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
