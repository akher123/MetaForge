using System.Security.Claims;
using MetaForge.Domain.Enums;
using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;

using MetaForge.UnitTests.Support;

namespace MetaForge.UnitTests;

public class UserAuthorizationSnapshotTests
{
    [Fact]
    public async Task GetSnapshotAsync_LoadsPermissionsFromDatabase_NotClaims()
    {
        await using var context = CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new UserAuthorizationSnapshotProvider(context, cache);
        var user = CreatePrincipal(context, "customer.View");

        var snapshot = await provider.GetSnapshotAsync(user);

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.HasPermission("customer.View"));
        Assert.False(snapshot.HasPermission("product.View"));
    }

    [Fact]
    public async Task HasFormPermissionAsync_ReflectsUpdatedPermissionsAfterStampBump()
    {
        await using var context = CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new UserAuthorizationSnapshotProvider(context, cache);
        var authService = new FormAuthorizationService(context, TestEntityTypeResolverFactory.Create(context), provider);
        var stampService = new SecurityStampService(context);

        var userEntity = context.Users.Single();
        var user = CreatePrincipal(userEntity);
        var auth = authService;

        Assert.True(await auth.HasFormPermissionAsync(user, "customer", PermissionAction.View));

        var permission = context.Permissions.Single(p => p.Code == "product.View");
        userEntity.UserRoles.Single().Role.RolePermissions.Add(new RolePermission { Permission = permission });
        await context.SaveChangesAsync();
        await stampService.BumpUserStampAsync(userEntity.Id);

        var refreshedUser = CreatePrincipal(await ReloadUser(context, userEntity.Id));
        Assert.False(await auth.HasFormPermissionAsync(user, "product", PermissionAction.View));
        Assert.True(await auth.HasFormPermissionAsync(refreshedUser, "product", PermissionAction.View));
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsNullWhenSecurityStampDoesNotMatch()
    {
        await using var context = CreateContext();
        var provider = new UserAuthorizationSnapshotProvider(context, new MemoryCache(new MemoryCacheOptions()));
        var userEntity = context.Users.Single();

        var stalePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
            new Claim(AppConstants.SecurityStampClaimType, "stale-stamp")
        ], "Cookies"));

        var snapshot = await provider.GetSnapshotAsync(stalePrincipal);

        Assert.Null(snapshot);
    }

    private static async Task<User> ReloadUser(MetaForgeDbContext context, int userId) =>
        await context.Users.AsNoTracking().FirstAsync(u => u.Id == userId);

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new MetaForgeDbContext(options);
        var role = new Role
        {
            Name = "TestRole",
            RolePermissions =
            [
                new RolePermission
                {
                    Permission = new Permission
                    {
                        Code = "customer.View",
                        Name = "Customer View",
                        Action = PermissionAction.View
                    }
                }
            ]
        };

        context.Users.Add(new User
        {
            UserName = "tester",
            Email = "test@localhost",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            UserRoles = [new UserRole { Role = role }]
        });

        context.Permissions.Add(new Permission
        {
            Code = "product.View",
            Name = "Product View",
            Action = PermissionAction.View
        });

        context.SaveChanges();
        return context;
    }

    private static ClaimsPrincipal CreatePrincipal(MetaForgeDbContext context, string permissionCode)
    {
        var user = context.Users.Single();
        return CreatePrincipal(user);
    }

    private static ClaimsPrincipal CreatePrincipal(User user) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(AppConstants.SecurityStampClaimType, user.SecurityStamp)
        ], "Cookies"));
}
