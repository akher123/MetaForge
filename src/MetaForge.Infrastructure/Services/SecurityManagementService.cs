using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// CRUD and assignment operations for users, roles, and permissions.
/// </summary>
public class SecurityManagementService : ISecurityManagementService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecurityStampService _securityStampService;

    public SecurityManagementService(
        MetaForgeDbContext dbContext,
        IUnitOfWork unitOfWork,
        ISecurityStampService securityStampService)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _securityStampService = securityStampService;
    }

    public async Task<SecurityOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        new()
        {
            UserCount = await _dbContext.Users.CountAsync(cancellationToken),
            ActiveUserCount = await _dbContext.Users.CountAsync(u => u.IsActive, cancellationToken),
            RoleCount = await _dbContext.Roles.CountAsync(cancellationToken),
            PermissionCount = await _dbContext.Permissions.CountAsync(cancellationToken)
        };

    public async Task<IReadOnlyList<UserManagementDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserManagementDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                IsActive = u.IsActive,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
            }).ToListAsync(cancellationToken);

    public async Task<UserManagementDto?> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return null;

        return new UserManagementDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsActive = user.IsActive,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }

    public async Task<SaveUserDto?> GetUserForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return null;

        return new SaveUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsActive = user.IsActive,
            RoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList()
        };
    }

    public async Task<int> SaveUserAsync(SaveUserDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName))
            throw new BusinessException("Username is required.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new BusinessException("Email is required.");

        var exists = await _dbContext.Users.AnyAsync(u =>
            u.UserName == dto.UserName && u.Id != dto.Id, cancellationToken);
        if (exists)
            throw new BusinessException($"Username '{dto.UserName}' already exists.");

        User user;
        if (dto.Id > 0)
        {
            user = await _dbContext.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == dto.Id, cancellationToken)
                ?? throw new NotFoundException($"User {dto.Id} not found.");

            user.UserName = dto.UserName.Trim();
            user.Email = dto.Email.Trim();
            user.IsActive = dto.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = PasswordHasher.Hash(dto.Password);

            _dbContext.UserRoles.RemoveRange(user.UserRoles);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new BusinessException("Password is required for new users.");

            user = new User
            {
                UserName = dto.UserName.Trim(),
                Email = dto.Email.Trim(),
                PasswordHash = PasswordHasher.Hash(dto.Password),
                IsActive = dto.IsActive
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
        }

        foreach (var roleId in dto.RoleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityStampService.BumpUserStampAsync(user.Id, cancellationToken);
        return user.Id;
    }

    public async Task DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException($"User {id} not found.");

        if (user.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("The default admin user cannot be deleted.");

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleManagementDto>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleManagementDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                UserCount = r.UserRoles.Count,
                PermissionCount = r.RolePermissions.Count
            }).ToListAsync(cancellationToken);

    public async Task<RoleDetailDto?> GetRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null) return null;

        var groups = await GetPermissionGroupsAsync(cancellationToken);

        return new RoleDetailDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            PermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList(),
            PermissionGroups = groups.ToList()
        };
    }

    public async Task<int> SaveRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Role name is required.");

        var exists = await _dbContext.Roles.AnyAsync(r =>
            r.Name == dto.Name && r.Id != dto.Id, cancellationToken);
        if (exists)
            throw new BusinessException($"Role '{dto.Name}' already exists.");

        Role role;
        if (dto.Id > 0)
        {
            role = await _dbContext.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Id == dto.Id, cancellationToken)
                ?? throw new NotFoundException($"Role {dto.Id} not found.");

            role.Name = dto.Name.Trim();
            role.Description = dto.Description?.Trim();
            _dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        }
        else
        {
            role = new Role
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim()
            };
            await _dbContext.Roles.AddAsync(role, cancellationToken);
        }

        foreach (var permissionId in dto.PermissionIds.Distinct())
        {
            role.RolePermissions.Add(new RolePermission { PermissionId = permissionId });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityStampService.BumpUsersInRoleAsync(role.Id, cancellationToken);
        return role.Id;
    }

    public async Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Role {id} not found.");

        if (role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("The Administrator role cannot be deleted.");

        if (role.UserRoles.Count > 0)
            throw new BusinessException($"Role '{role.Name}' is assigned to {role.UserRoles.Count} user(s). Remove assignments first.");

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionGroupDto>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _dbContext.Permissions.AsNoTracking().OrderBy(p => p.Code).ToListAsync(cancellationToken);
        var modules = await _unitOfWork.Forms.GetActiveFormsAsync(cancellationToken);
        var moduleByCode = modules.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        return GroupPermissions(permissions, moduleByCode);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Action = p.Action,
                FormCode = ExtractFormCode(p.Code),
                RoleCount = p.RolePermissions.Count
            }).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleOptionDto { Id = r.Id, Name = r.Name })
            .ToListAsync(cancellationToken);

    public async Task<int> SyncFormPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _unitOfWork.Forms.GetActiveFormsAsync(cancellationToken);
        var existingPermissions = await _dbContext.Permissions.ToListAsync(cancellationToken);
        var existingSet = existingPermissions.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var adminRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator", cancellationToken);
        var adminPermissionIds = adminRole == null
            ? []
            : await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == adminRole.Id)
                .Select(rp => rp.PermissionId)
                .ToHashSetAsync(cancellationToken);

        var added = 0;

        foreach (var module in modules)
        {
            foreach (var action in PermissionAction.All)
            {
                var code = $"{module.Code}.{action}";
                if (existingSet.Contains(code)) continue;

                var permission = new Permission
                {
                    FormId = module.Id,
                    Action = action,
                    Code = code,
                    Name = $"{module.Name} - {action}"
                };
                _dbContext.Permissions.Add(permission);

                if (adminRole != null)
                    _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

                existingSet.Add(code);
                added++;
            }
        }

        foreach (var (code, name, action) in SecurityPermissions.All)
        {
            if (existingSet.Contains(code)) continue;

            var permission = new Permission
            {
                FormId = 0,
                Action = action,
                Code = code,
                Name = name
            };
            _dbContext.Permissions.Add(permission);

            if (adminRole != null && !adminPermissionIds.Contains(permission.Id))
                _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

            added++;
        }

        foreach (var (code, name, action) in ConfigPermissions.All)
        {
            if (existingSet.Contains(code)) continue;

            var permission = new Permission
            {
                FormId = 0,
                Action = action,
                Code = code,
                Name = name
            };
            _dbContext.Permissions.Add(permission);

            if (adminRole != null)
                _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

            existingSet.Add(code);
            added++;
        }

        foreach (var (code, name, action) in ReportConfigPermissions.All)
        {
            if (existingSet.Contains(code)) continue;

            var permission = new Permission
            {
                FormId = 0,
                Action = action,
                Code = code,
                Name = name
            };
            _dbContext.Permissions.Add(permission);

            if (adminRole != null)
                _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

            existingSet.Add(code);
            added++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (adminRole != null && added > 0)
            await _securityStampService.BumpUsersInRoleAsync(adminRole.Id, cancellationToken);

        return added;
    }

    public async Task<int> SyncReportPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var reports = await _unitOfWork.Reports.GetActiveReportsAsync(cancellationToken);
        var existingPermissions = await _dbContext.Permissions.ToListAsync(cancellationToken);
        var existingSet = existingPermissions.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var adminRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator", cancellationToken);

        var added = 0;

        foreach (var report in reports)
        {
            foreach (var action in ReportPermissionAction.All)
            {
                var code = $"{report.Code}.{action}";
                if (existingSet.Contains(code))
                    continue;

                var permission = new Permission
                {
                    FormId = 0,
                    Action = action,
                    Code = code,
                    Name = $"{report.Name} - {action}"
                };
                _dbContext.Permissions.Add(permission);

                if (adminRole != null)
                    _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

                existingSet.Add(code);
                added++;
            }
        }

        foreach (var (code, name, action) in ReportConfigPermissions.All)
        {
            if (existingSet.Contains(code))
                continue;

            var permission = new Permission
            {
                FormId = 0,
                Action = action,
                Code = code,
                Name = name
            };
            _dbContext.Permissions.Add(permission);

            if (adminRole != null)
                _dbContext.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });

            existingSet.Add(code);
            added++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (adminRole != null && added > 0)
            await _securityStampService.BumpUsersInRoleAsync(adminRole.Id, cancellationToken);

        return added;
    }

    private static List<PermissionGroupDto> GroupPermissions(
        List<Permission> permissions,
        IReadOnlyDictionary<string, Domain.Metadata.ForgeForm> moduleByCode)
    {
        return permissions
            .GroupBy(p => ExtractFormCode(p.Code))
            .Select(g =>
            {
                var moduleCode = g.Key;
                moduleByCode.TryGetValue(moduleCode, out var module);
                return new PermissionGroupDto
                {
                    CategoryName = ResolveCategoryName(moduleCode, module),
                    FormCode = moduleCode,
                    GroupName = module?.Name ?? FormatGroupName(moduleCode),
                    Permissions = g.Select(p => new PermissionDto
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Action = p.Action,
                        FormCode = moduleCode
                    }).OrderBy(p => p.Action).ToList()
                };
            })
            .OrderBy(g => CategorySortOrder(g.CategoryName))
            .ThenBy(g => g.GroupName)
            .ToList();
    }

    private static string ResolveCategoryName(string moduleCode, Domain.Metadata.ForgeForm? module)
    {
        if (moduleCode.Equals(SecurityPermissions.FormCode, StringComparison.OrdinalIgnoreCase)
            || moduleCode.Equals(ConfigPermissions.FormCode, StringComparison.OrdinalIgnoreCase)
            || moduleCode.Equals(ReportConfigPermissions.FormCode, StringComparison.OrdinalIgnoreCase))
        {
            return "System Administration";
        }

        return string.IsNullOrWhiteSpace(module?.GroupName) ? "Modules" : module.GroupName;
    }

    private static int CategorySortOrder(string category) => category switch
    {
        "System Administration" => 0,
        "Master Data" => 1,
        "Transaction" => 2,
        _ => 3
    };

    private static string ExtractFormCode(string permissionCode)
    {
        var dot = permissionCode.LastIndexOf('.');
        return dot > 0 ? permissionCode[..dot] : permissionCode;
    }

    private static string FormatGroupName(string moduleCode) =>
        moduleCode.Equals(SecurityPermissions.FormCode, StringComparison.OrdinalIgnoreCase)
            ? "Security Management"
            : moduleCode.Equals(ConfigPermissions.FormCode, StringComparison.OrdinalIgnoreCase)
                ? "Form Builder"
                : moduleCode.Equals(ReportConfigPermissions.FormCode, StringComparison.OrdinalIgnoreCase)
                    ? "Report Builder"
                    : char.ToUpper(moduleCode[0]) + moduleCode[1..];
}
