using System;
using System.Collections.Generic;
using System.Text;

namespace MetaForge.Domain.Business;

public class Department : BaseEntity, IForgeBusinessEntity
{
    public string DepartmentCode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string HeadOfDepartment { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string ContactPhone { get; set; } = string.Empty;

    public DateTime EstablishedDate { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
