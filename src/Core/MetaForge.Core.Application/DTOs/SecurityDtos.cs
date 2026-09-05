namespace MetaForge.Application.DTOs;

public class UserManagementDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];
}

public class SaveUserDto
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public List<int> RoleIds { get; set; } = [];
}

public class RoleManagementDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int UserCount { get; set; }

    public int PermissionCount { get; set; }
}

public class SaveRoleDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<int> PermissionIds { get; set; } = [];
}

public class RoleDetailDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<int> PermissionIds { get; set; } = [];

    public List<PermissionGroupDto> PermissionGroups { get; set; } = [];
}

public class PermissionDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string FormCode { get; set; } = string.Empty;

    public int RoleCount { get; set; }
}

public class PermissionGroupDto
{
    public string CategoryName { get; set; } = "Forms";

    public string GroupName { get; set; } = string.Empty;

    public string FormCode { get; set; } = string.Empty;

    public List<PermissionDto> Permissions { get; set; } = [];
}

public class SecurityOverviewDto
{
    public int UserCount { get; set; }

    public int ActiveUserCount { get; set; }

    public int RoleCount { get; set; }

    public int PermissionCount { get; set; }
}

public class RoleOptionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
