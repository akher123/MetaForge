using System.Security.Claims;
using MetaForge.Domain.Enums;
using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Exceptions;

namespace MetaForge.UnitTests;

public class LookupAuthorizationTests
{
    [Fact]
    public async Task CanAccessLookupAsync_DeniesSecurityEntities()
    {
        await using var context = CreateContext();
        var (service, user) = CreateAuthorization(context, "customer.View");

        Assert.False(await service.CanAccessLookupAsync(user, "User"));
        Assert.False(await service.CanAccessLookupAsync(user, "Role"));
    }

    [Fact]
    public async Task CanAccessLookupAsync_AllowsWhenUserHasViewOnReferencingForm()
    {
        await using var context = CreateContext(withCustomerForm: true);
        var (service, user) = CreateAuthorization(context, "customer.View");

        Assert.True(await service.CanAccessLookupAsync(user, "Country"));
    }

    [Fact]
    public async Task CanAccessLookupAsync_DeniesWithoutMatchingFormPermission()
    {
        await using var context = CreateContext(withCustomerForm: true);
        var (service, user) = CreateAuthorization(context, "product.View");

        Assert.False(await service.CanAccessLookupAsync(user, "Country"));
    }

    [Fact]
    public async Task CanAccessLookupAsync_AllowsDetailLookupViaParentFormPermission()
    {
        await using var context = CreateContext(withSalesOrderForms: true);
        var (service, user) = CreateAuthorization(context, "salesorder.View");

        Assert.True(await service.CanAccessLookupAsync(user, "Product"));
    }

    [Fact]
    public async Task CanAccessLookupAsync_AllowsFormBuilderUsers()
    {
        await using var context = CreateContext();
        var (service, user) = CreateAuthorization(context, ConfigPermissions.View);

        Assert.True(await service.CanAccessLookupAsync(user, "Country"));
    }

    [Fact]
    public async Task GetLookupItemsAsync_RejectsSecurityEntity()
    {
        await using var context = CreateContext();
        var service = new LookupService(context, new EntityTypeResolver(context), new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAsync<BusinessException>(() => service.GetLookupItemsAsync("User"));
    }

    private static (FormAuthorizationService Service, ClaimsPrincipal User) CreateAuthorization(
        MetaForgeDbContext context,
        params string[] permissionCodes)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshotProvider = new UserAuthorizationSnapshotProvider(context, cache);
        var service = new FormAuthorizationService(context, new EntityTypeResolver(context), snapshotProvider);
        var user = CreatePrincipal(context, permissionCodes);
        return (service, user);
    }

    private static ClaimsPrincipal CreatePrincipal(MetaForgeDbContext context, params string[] permissionCodes)
    {
        var role = new Role { Name = "TestRole" };
        foreach (var code in permissionCodes)
        {
            var action = code.Contains('.') ? code[(code.LastIndexOf('.') + 1)..] : "View";
            role.RolePermissions.Add(new RolePermission
            {
                Permission = new Permission
                {
                    Code = code,
                    Name = code,
                    Action = action
                }
            });
        }

        var user = new User
        {
            UserName = Guid.NewGuid().ToString("N"),
            Email = "test@localhost",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            UserRoles = [new UserRole { Role = role }]
        };

        context.Users.Add(user);
        context.SaveChanges();

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(AppConstants.SecurityStampClaimType, user.SecurityStamp)
        ], "Cookies"));
    }

    private static MetaForgeDbContext CreateContext(bool withCustomerForm = false, bool withSalesOrderForms = false)
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new MetaForgeDbContext(options);
        context.Countries.Add(new Country { Code = "US", Name = "United States" });
        context.Products.Add(new Product { Code = "P1", Name = "Product 1", UnitPrice = 10m });

        if (withCustomerForm)
        {
            context.ForgeForms.Add(new ForgeForm
            {
                Code = "customer",
                Name = "Customer",
                EntityName = "Customer",
                TableName = "Customers",
                FormType = FormType.Master,
                IsActive = true,
                Fields =
                [
                    new ForgeField
                    {
                        PropertyName = "CountryId",
                        Label = "Country",
                        LookupEntity = "Country",
                        DisplayOrder = 0
                    }
                ]
            });
        }

        if (withSalesOrderForms)
        {
            context.ForgeForms.AddRange(
                new ForgeForm
                {
                    Code = "salesorder",
                    Name = "Sales Order",
                    EntityName = "SalesOrder",
                    TableName = "SalesOrders",
                    FormType = FormType.MasterDetailTabular,
                    IsActive = true,
                    Relations =
                    [
                        new ForgeRelation
                        {
                            ChildEntity = "SalesOrderItem",
                            RelationType = RelationType.OneToMany,
                            ForeignKey = "SalesOrderId"
                        }
                    ]
                },
                new ForgeForm
                {
                    Code = "salesorderitem",
                    Name = "Sales Order Item",
                    EntityName = "SalesOrderItem",
                    TableName = "SalesOrderItems",
                    FormType = FormType.Detail,
                    IsActive = true,
                    Fields =
                    [
                        new ForgeField
                        {
                            PropertyName = "ProductId",
                            Label = "Product",
                            LookupEntity = "Product",
                            DisplayOrder = 0
                        }
                    ]
                });
        }

        context.SaveChanges();
        return context;
    }
}
