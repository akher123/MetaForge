using MetaForge.Domain.Business;
using MetaForge.Domain.Security;
using MetaForge.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds metadata, security, and sample business data.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task ResetAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetaForgeDbContext>>();

        logger.LogWarning("Dropping database...");
        await context.Database.EnsureDeletedAsync();
        logger.LogInformation("Database dropped. Applying migrations and seeding...");
        await DatabaseMigrator.MigrateAsync(context, logger);
        await SeedDataAsync(scope, context, logger);
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetaForgeDbContext>>();

        await DatabaseMigrator.MigrateAsync(context, logger);

        if (await context.ForgeForms.AnyAsync())
        {
            logger.LogInformation("Database already seeded.");
            await ApplyDataUpgradesAsync(context, scope, logger);
            return;
        }

        await SeedDataAsync(scope, context, logger);
    }

    private static async Task SeedDataAsync(IServiceScope scope, MetaForgeDbContext context, ILogger logger)
    {
        SeedBusinessData(context);
        SeedMetadata(context);
        SeedLookups(context);
        SeedSecurity(context);

        await context.SaveChangesAsync();
        await ApplyDataUpgradesAsync(context, scope, logger);
        logger.LogInformation("Database seeded successfully.");
    }

    private static async Task ApplyDataUpgradesAsync(
        MetaForgeDbContext context,
        IServiceScope scope,
        ILogger logger)
    {
        await EnsureUserSecurityStampsAsync(context, logger);
        await UpgradeLegacyPasswordsAsync(context, logger);
        await EnsureSecurityPermissionsAsync(context, logger);
        await EnsureFormPermissionsAsync(context, logger);
        await EnsureCascadeLookupUpgradeAsync(context, logger);
        await EnsurePagedLookupUpgradeAsync(context, logger);
        await EnsureSampleCustomerAsync(context, logger);
        await EnsureSampleTransactionDataAsync(context, logger);
        await EnsureTabularSalesOrderUpgradeAsync(context, logger);
        await EnsureSalesOrderGridActionsAsync(context, logger);
        await EnsureFormPermissionsAsync(context, logger);
        await EnsureMenusAsync(scope, logger);
    }

    private static async Task EnsureMenusAsync(IServiceScope scope, ILogger logger)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();

            await context.ForgeMenus
                .Where(m => m.Action == "MasterDetail")
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Action, "Index"));

            var menuSync = scope.ServiceProvider.GetRequiredService<IMenuSyncService>();
            await menuSync.EnsureDefaultMenusAsync();
            logger.LogInformation("Navigation menus ensured.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure navigation menus.");
        }
    }

    private static async Task EnsureUserSecurityStampsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var users = await context.Users
            .Where(u => u.SecurityStamp == null || u.SecurityStamp == string.Empty)
            .ToListAsync();

        if (users.Count == 0)
            return;

        foreach (var user in users)
            user.SecurityStamp = Guid.NewGuid().ToString("N");

        await context.SaveChangesAsync();
        logger.LogInformation("Assigned security stamps to {Count} user(s).", users.Count);
    }

    private static async Task EnsureTabularSalesOrderUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        var salesOrderForm = await context.ForgeForms
            .Include(f => f.Relations)
            .Include(f => f.Fields)
            .Include(f => f.GridColumns)
            .FirstOrDefaultAsync(f => f.Code == "salesorder");

        if (salesOrderForm == null)
            return;

        var changed = false;

        if (salesOrderForm.FormType != FormType.MasterDetailTabular)
        {
            salesOrderForm.FormType = FormType.MasterDetailTabular;
            changed = true;
        }

        var itemsRelation = salesOrderForm.Relations.FirstOrDefault(r =>
            r.RelationType == RelationType.OneToMany
            && r.ChildEntity.Equals("SalesOrderItem", StringComparison.OrdinalIgnoreCase));

        if (itemsRelation != null)
        {
            if (string.IsNullOrWhiteSpace(itemsRelation.TabLabel))
            {
                itemsRelation.TabLabel = "Line Items";
                changed = true;
            }

            if (itemsRelation.DisplayOrder != 0)
            {
                itemsRelation.DisplayOrder = 0;
                changed = true;
            }
        }

        var chargesRelation = salesOrderForm.Relations.FirstOrDefault(r =>
            r.RelationType == RelationType.OneToMany
            && r.ChildEntity.Equals("SalesOrderCharge", StringComparison.OrdinalIgnoreCase));

        if (chargesRelation == null)
        {
            salesOrderForm.Relations.Add(new ForgeRelation
            {
                RelationType = RelationType.OneToMany,
                ParentEntity = "SalesOrder",
                ChildEntity = "SalesOrderCharge",
                ForeignKey = "SalesOrderId",
                NavigationProperty = "Charges",
                TabLabel = "Charges",
                DisplayOrder = 1
            });
            changed = true;
        }

        if (!await context.ForgeForms.AnyAsync(f => f.Code == "salesordercharge"))
        {
            context.ForgeForms.Add(BuildForm(
                "salesordercharge", "Sales Order Charge", "SalesOrderCharge", "SalesOrderCharges", "Transaction", 3, FormType.Detail,
                fields:
                [
                    ("SalesOrderId", ControlType.Dropdown, true, null, "SalesOrder"),
                    ("ChargeType", ControlType.TextBox, true, null, null),
                    ("Description", ControlType.TextArea, false, null, null),
                    ("Amount", ControlType.Number, true, null, null)
                ],
                grid: ["ChargeType", "Description", "Amount"]));
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded Sales Order to tabular master-detail sample.");
        }

        var order = await context.SalesOrders
            .Include(o => o.Charges)
            .OrderBy(o => o.Id)
            .FirstOrDefaultAsync();

        if (order != null && order.Charges.Count == 0)
        {
            order.Charges.Add(new SalesOrderCharge { ChargeType = "Freight", Description = "Standard shipping", Amount = 15.00m });
            order.Charges.Add(new SalesOrderCharge { ChargeType = "Tax", Description = "Sales tax", Amount = 8.50m });
            await context.SaveChangesAsync();
            logger.LogInformation("Added sample sales order charges.");
        }
    }

    private static async Task EnsureSalesOrderGridActionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var salesOrderForm = await context.ForgeForms
            .Include(f => f.GridActions)
            .FirstOrDefaultAsync(f => f.Code == "salesorder");

        if (salesOrderForm == null)
            return;

        if (salesOrderForm.GridActions.Any(a => a.Code == "approve"))
            return;

        salesOrderForm.GridActions.Add(new ForgeFormAction
        {
            Code = "approve",
            Label = "Approve",
            Icon = "check",
            Placement = GridActionPlacement.Row,
            HandlerType = GridActionHandlerType.Api,
            HandlerTarget = "/api/metaforge/crud/SalesOrder/{id}",
            HttpMethod = "PUT",
            RequestBody = """{"Status":"Approved"}""",
            PermissionAction = PermissionAction.Approve,
            ConfirmMessage = "Approve this sales order?",
            ButtonStyle = "outline-success",
            DisplayOrder = 0,
            IsActive = true
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Added Approve row action to Sales Order grid.");
    }

    private static async Task EnsureCascadeLookupUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        await EnsureSampleRegionsAsync(context, logger);
        await FixInvalidCustomerRegionReferencesAsync(context, logger);

        if (!await context.LookupConfigurations.AnyAsync(c => c.EntityName == "Region"))
        {
            context.LookupConfigurations.Add(new LookupConfiguration { EntityName = "Region", ValueField = "Id", TextField = "Name" });
            await context.SaveChangesAsync();
        }

        var customerForm = await context.ForgeForms
            .Include(f => f.Fields)
            .Include(f => f.GridColumns)
            .FirstOrDefaultAsync(f => f.Code == "customer");

        if (customerForm == null)
            return;

        var changed = false;
        var regionField = customerForm.Fields.FirstOrDefault(f => f.PropertyName == "RegionId");
        if (regionField == null)
        {
            customerForm.Fields.Add(new ForgeField
            {
                PropertyName = "RegionId",
                Label = "Region",
                ControlType = ControlType.Dropdown,
                IsRequired = false,
                IsVisible = true,
                DisplayOrder = customerForm.Fields.Count,
                LookupEntity = "Region",
                LookupParentField = "CountryId"
            });
            changed = true;
        }
        else if (string.IsNullOrWhiteSpace(regionField.LookupParentField))
        {
            regionField.LookupParentField = "CountryId";
            regionField.LookupEntity ??= "Region";
            changed = true;
        }

        if (!customerForm.GridColumns.Any(c => c.PropertyName == "RegionId"))
        {
            customerForm.GridColumns.Add(new ForgeGridColumn
            {
                PropertyName = "RegionId",
                Label = "Region",
                DisplayOrder = customerForm.GridColumns.Count,
                IsSortable = true,
                IsSearchable = false,
                IsVisible = true
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded Customer form with cascading Region lookup.");
        }

        var customer = await context.Customers
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.RegionId == null && c.CountryId != null);

        if (customer != null)
        {
            var regionId = await context.Regions
                .Where(r => r.CountryId == customer.CountryId)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (regionId > 0)
            {
                customer.RegionId = regionId;
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task EnsurePagedLookupUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        var largeLookupEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Product", "Customer", "SalesOrder", "Supplier"
        };

        var forms = await context.ForgeForms
            .Include(f => f.Fields)
            .ToListAsync();

        var changed = false;
        foreach (var form in forms)
        {
            foreach (var field in form.Fields)
            {
                var isForeignKey = field.PropertyName.EndsWith("Id", StringComparison.Ordinal)
                    && !field.PropertyName.Equals("Id", StringComparison.Ordinal);
                var lookupEntity = field.LookupEntity
                    ?? (isForeignKey ? field.PropertyName[..^2] : null);

                if (lookupEntity == null)
                    continue;

                if (field.ControlType == ControlType.Dropdown
                    && (largeLookupEntities.Contains(lookupEntity) || isForeignKey))
                {
                    field.ControlType = ControlType.Autocomplete;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded lookup fields to Autocomplete with paged search for large datasets.");
        }
    }

    private static async Task EnsureSampleRegionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var countries = await context.Countries
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        if (countries.Count == 0)
            return;

        var existingCodes = (await context.Regions
            .Select(r => r.Code)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sampleRegions = new (string Code, string Name, string CountryCode)[]
        {
            ("US-W", "West", "US"),
            ("US-E", "East", "US"),
            ("UK-L", "London", "UK"),
            ("DE-B", "Bavaria", "DE")
        };

        var added = false;
        foreach (var sample in sampleRegions)
        {
            if (existingCodes.Contains(sample.Code))
                continue;

            if (!countries.TryGetValue(sample.CountryCode, out var countryId))
                continue;

            context.Regions.Add(new Region
            {
                Code = sample.Code,
                Name = sample.Name,
                CountryId = countryId
            });
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Ensured sample regions exist for cascading lookups.");
        }
    }

    private static async Task FixInvalidCustomerRegionReferencesAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (!await context.Regions.AnyAsync())
        {
            var cleared = await context.Customers
                .Where(c => c.RegionId != null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.RegionId, (int?)null));

            if (cleared > 0)
                logger.LogInformation("Cleared RegionId on {Count} customer(s) because no regions exist.", cleared);

            return;
        }

        var validRegionIds = await context.Regions.Select(r => r.Id).ToListAsync();
        var customers = await context.Customers
            .Where(c => c.RegionId != null)
            .ToListAsync();

        var fixedCount = 0;
        foreach (var customer in customers)
        {
            if (customer.RegionId is > 0 && !validRegionIds.Contains(customer.RegionId.Value))
            {
                customer.RegionId = null;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Cleared invalid RegionId on {Count} customer(s).", fixedCount);
        }
    }

    private static async Task EnsureSecurityPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var existing = await context.Permissions.Select(p => p.Code).ToListAsync();
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (code, name, action) in Shared.Constants.SecurityPermissions.All)
        {
            if (set.Contains(code)) continue;
            var perm = new Permission { Action = action, Code = code, Name = name };
            context.Permissions.Add(perm);
            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = perm });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} security permissions.", added);
        }
    }

    private static async Task EnsureFormPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var modules = await context.ForgeForms.Where(m => m.IsActive).AsNoTracking().ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var module in modules)
        {
            foreach (var action in PermissionAction.All)
            {
                var code = $"{module.Code}.{action}";
                if (!permissionByCode.TryGetValue(code, out var permission))
                {
                    permission = new Permission
                    {
                        FormId = module.Id,
                        Action = action,
                        Code = code,
                        Name = $"{module.Name} - {action}"
                    };
                    context.Permissions.Add(permission);
                    permissionByCode[code] = permission;
                    addedPermissions++;
                }

                if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                    continue;

                if (permission.Id > 0)
                {
                    var alreadyAssigned = await context.RolePermissions.AnyAsync(
                        rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
                    if (alreadyAssigned)
                    {
                        adminPermissionIds.Add(permission.Id);
                        continue;
                    }
                }

                context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
                if (permission.Id > 0)
                    adminPermissionIds.Add(permission.Id);
                addedAssignments++;
            }
        }

        if (addedPermissions > 0 || addedAssignments > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Synced module permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static async Task EnsureSampleCustomerAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (await context.Customers.AnyAsync())
            return;

        if (!await context.Countries.AnyAsync())
            return;

        await EnsureSampleRegionsAsync(context, logger);

        var countryId = await context.Countries.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
        var regionId = await context.Regions
            .Where(r => r.CountryId == countryId)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();

        context.Customers.Add(new Customer
        {
            Code = "C001",
            Name = "Contoso Ltd",
            Email = "info@contoso.com",
            Status = "Active",
            CountryId = countryId,
            RegionId = regionId,
            Address = new Address { Street = "123 Main St", City = "Seattle", CountryId = countryId }
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Added sample customer for transaction screens.");
    }

    private static async Task EnsureSampleTransactionDataAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (await context.SalesOrders.AnyAsync())
            return;

        if (!await context.Customers.AnyAsync() || !await context.Products.AnyAsync())
            return;

        var customerId = await context.Customers.Select(c => c.Id).FirstAsync();
        var products = await context.Products.OrderBy(p => p.Id).Take(2).Select(p => new { p.Id, p.UnitPrice }).ToListAsync();
        if (products.Count == 0)
            return;

        context.SalesOrders.Add(new SalesOrder
        {
            OrderNo = "SO-001",
            OrderDate = DateTime.UtcNow.Date,
            CustomerId = customerId,
            Status = "Draft",
            Items =
            [
                new SalesOrderItem { ProductId = products[0].Id, Quantity = 2, UnitPrice = products[0].UnitPrice },
                new SalesOrderItem
                {
                    ProductId = products.Count > 1 ? products[1].Id : products[0].Id,
                    Quantity = 1,
                    UnitPrice = products.Count > 1 ? products[1].UnitPrice : products[0].UnitPrice
                }
            ],
            Charges =
            [
                new SalesOrderCharge { ChargeType = "Freight", Description = "Standard shipping", Amount = 15.00m },
                new SalesOrderCharge { ChargeType = "Tax", Description = "Sales tax", Amount = 8.50m }
            ]
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Added sample sales order transaction data.");
    }

    private static async Task UpgradeLegacyPasswordsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminPasswordHash = await context.Users
            .AsNoTracking()
            .Where(u => u.UserName == "admin")
            .Select(u => u.PasswordHash)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(adminPasswordHash) || !PasswordHasher.IsLegacyHash(adminPasswordHash))
            return;

        var admin = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");
        if (admin == null)
            return;

        admin.PasswordHash = PasswordHasher.Hash("admin");
        await context.SaveChangesAsync();
        logger.LogInformation("Upgraded legacy admin password hash.");
    }

    private static void SeedBusinessData(MetaForgeDbContext context)
    {
        var us = new Country { Code = "US", Name = "United States" };
        var uk = new Country { Code = "UK", Name = "United Kingdom" };
        var de = new Country { Code = "DE", Name = "Germany" };
        context.Countries.AddRange(us, uk, de);

        var usWest = new Region { Code = "US-W", Name = "West", Country = us };
        var usEast = new Region { Code = "US-E", Name = "East", Country = us };
        var ukLondon = new Region { Code = "UK-L", Name = "London", Country = uk };
        var deBavaria = new Region { Code = "DE-B", Name = "Bavaria", Country = de };
        context.Regions.AddRange(usWest, usEast, ukLondon, deBavaria);

        context.Products.AddRange(
            new Product { Code = "P001", Name = "Widget A", UnitPrice = 19.99m },
            new Product { Code = "P002", Name = "Widget B", UnitPrice = 29.99m });

        var product1 = context.Products.Local.First(p => p.Code == "P001");
        var product2 = context.Products.Local.First(p => p.Code == "P002");

        context.Suppliers.Add(
            new Supplier { Code = "S001", Name = "Acme Supplies", ContactEmail = "sales@acme.com" });

        var customer = new Customer
        {
            Code = "C001",
            Name = "Contoso Ltd",
            Email = "info@contoso.com",
            Status = "Active",
            Country = us,
            Region = usWest,
            Address = new Address { Street = "123 Main St", City = "Seattle", Country = us }
        };
        context.Customers.Add(customer);

        context.SalesOrders.Add(new SalesOrder
        {
            OrderNo = "SO-001",
            OrderDate = DateTime.UtcNow.Date,
            Customer = customer,
            Status = "Draft",
            Items =
            [
                new SalesOrderItem { Product = product1, Quantity = 2, UnitPrice = product1.UnitPrice },
                new SalesOrderItem { Product = product2, Quantity = 1, UnitPrice = product2.UnitPrice }
            ],
            Charges =
            [
                new SalesOrderCharge { ChargeType = "Freight", Description = "Standard shipping", Amount = 15.00m },
                new SalesOrderCharge { ChargeType = "Tax", Description = "Sales tax", Amount = 8.50m }
            ]
        });
    }

    private static void SeedMetadata(MetaForgeDbContext context)
    {
        context.ForgeForms.AddRange(
            BuildForm("country", "Country", "Country", "Countries", "Master Data", 1, FormType.Master,
                fields: [("Code", ControlType.TextBox, true, null, null), ("Name", ControlType.TextBox, true, null, null), ("IsActive", ControlType.Checkbox, false, null, null)],
                grid: ["Code", "Name", "IsActive"]),

            BuildForm("customer", "Customer", "Customer", "Customers", "Master Data", 2, FormType.Master,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("Email", ControlType.TextBox, false, "Email", null),
                    ("Status", ControlType.TextBox, false, null, null),
                    ("CountryId", ControlType.Dropdown, false, null, "Country"),
                    ("RegionId", ControlType.Dropdown, false, null, "Region")
                ],
                grid: ["Code", "Name", "Email", "CountryId", "RegionId"],
                relations:
                [
                    new ForgeRelation
                    {
                        RelationType = RelationType.OneToOne,
                        ParentEntity = "Customer",
                        ChildEntity = "Address",
                        ForeignKey = "CustomerId",
                        NavigationProperty = "Address"
                    }
                ],
                cascadeFields: new Dictionary<string, (string CascadeFrom, string? FilterField)>
                {
                    ["RegionId"] = ("CountryId", null)
                }),

            BuildForm("product", "Product", "Product", "Products", "Master Data", 3, FormType.Master,
                fields: [("Code", ControlType.TextBox, true, null, null), ("Name", ControlType.TextBox, true, null, null), ("UnitPrice", ControlType.Number, true, null, null), ("IsActive", ControlType.Checkbox, false, null, null)],
                grid: ["Code", "Name", "UnitPrice", "IsActive"]),

            BuildForm("supplier", "Supplier", "Supplier", "Suppliers", "Master Data", 4, FormType.Master,
                fields: [("Code", ControlType.TextBox, true, null, null), ("Name", ControlType.TextBox, true, null, null), ("ContactEmail", ControlType.TextBox, false, null, null), ("IsActive", ControlType.Checkbox, false, null, null)],
                grid: ["Code", "Name", "ContactEmail", "IsActive"]),

            BuildForm("salesorder", "Sales Order", "SalesOrder", "SalesOrders", "Transaction", 1, FormType.MasterDetailTabular,
                fields:
                [
                    ("OrderNo", ControlType.TextBox, true, null, null),
                    ("OrderDate", ControlType.DateTime, true, null, null),
                    ("CustomerId", ControlType.Autocomplete, true, null, "Customer"),
                    ("Status", ControlType.TextBox, false, null, null)
                ],
                grid: ["OrderNo", "OrderDate", "CustomerId", "Status"],
                relations:
                [
                    new ForgeRelation
                    {
                        RelationType = RelationType.OneToMany,
                        ParentEntity = "SalesOrder",
                        ChildEntity = "SalesOrderItem",
                        ForeignKey = "SalesOrderId",
                        NavigationProperty = "Items",
                        TabLabel = "Line Items",
                        DisplayOrder = 0
                    },
                    new ForgeRelation
                    {
                        RelationType = RelationType.OneToMany,
                        ParentEntity = "SalesOrder",
                        ChildEntity = "SalesOrderCharge",
                        ForeignKey = "SalesOrderId",
                        NavigationProperty = "Charges",
                        TabLabel = "Charges",
                        DisplayOrder = 1
                    }
                ]),

            BuildForm("salesorderitem", "Sales Order Item", "SalesOrderItem", "SalesOrderItems", "Transaction", 2, FormType.Detail,
                fields:
                [
                    ("SalesOrderId", ControlType.Dropdown, true, null, "SalesOrder"),
                    ("ProductId", ControlType.Autocomplete, true, null, "Product"),
                    ("Quantity", ControlType.Number, true, null, null),
                    ("UnitPrice", ControlType.Number, true, null, null)
                ],
                grid: ["SalesOrderId", "ProductId", "Quantity", "UnitPrice"]),

            BuildForm("salesordercharge", "Sales Order Charge", "SalesOrderCharge", "SalesOrderCharges", "Transaction", 3, FormType.Detail,
                fields:
                [
                    ("SalesOrderId", ControlType.Dropdown, true, null, "SalesOrder"),
                    ("ChargeType", ControlType.TextBox, true, null, null),
                    ("Description", ControlType.TextArea, false, null, null),
                    ("Amount", ControlType.Number, true, null, null)
                ],
                grid: ["ChargeType", "Description", "Amount"]));
    }

    private static ForgeForm BuildForm(
        string code, string name, string entityName, string tableName, string group, int order, FormType formType,
        (string Property, string Control, bool Required, string? Validation, string? Lookup)[] fields,
        string[] grid,
        List<ForgeRelation>? relations = null,
        Dictionary<string, (string CascadeFrom, string? FilterField)>? cascadeFields = null)
    {
        return new ForgeForm
        {
            Code = code,
            Name = name,
            EntityName = entityName,
            TableName = tableName,
            GroupName = group,
            FormType = formType,
            DisplayOrder = order,
            IsActive = true,
            Fields = fields.Select((f, i) =>
            {
                string? cascadeFrom = null;
                string? filterField = null;
                if (cascadeFields != null && cascadeFields.TryGetValue(f.Property, out var cascade))
                {
                    cascadeFrom = cascade.CascadeFrom;
                    filterField = cascade.FilterField;
                }

                return new ForgeField
                {
                    PropertyName = f.Property,
                    Label = f.Lookup ?? f.Property,
                    ControlType = f.Control,
                    IsRequired = f.Required,
                    IsVisible = true,
                    DisplayOrder = i,
                    ValidationRule = f.Validation ?? (f.Required ? "Required" : null),
                    LookupEntity = f.Lookup,
                    LookupParentField = cascadeFrom,
                    LookupFilterField = filterField
                };
            }).ToList(),
            GridColumns = grid.Select((c, i) =>
            {
                var field = fields.FirstOrDefault(f => f.Property == c);
                var hasField = fields.Any(f => f.Property == c);
                return new ForgeGridColumn
                {
                    PropertyName = c,
                    Label = hasField ? (field.Lookup ?? field.Property) : c,
                    DisplayOrder = i,
                    IsSortable = true,
                    IsSearchable = c is "Code" or "Name" or "Email" or "OrderNo"
                };
            }).ToList(),
            Relations = relations ?? []
        };
    }

    private static void SeedLookups(MetaForgeDbContext context)
    {
        context.LookupConfigurations.AddRange(
            new LookupConfiguration { EntityName = "Country", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "Region", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "Customer", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "Product", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "SalesOrder", ValueField = "Id", TextField = "OrderNo" });
    }

    private static void SeedSecurity(MetaForgeDbContext context)
    {
        var adminRole = new Role { Name = "Administrator", Description = "Full access" };
        context.Roles.Add(adminRole);

        context.Users.Add(new User
        {
            UserName = "admin",
            Email = "admin@localhost",
            PasswordHash = PasswordHasher.Hash("admin"),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            UserRoles = [new UserRole { Role = adminRole }]
        });

        var moduleCodes = context.ForgeForms.Local.Select(m => m.Code).ToList();
        foreach (var module in moduleCodes)
        {
            foreach (var action in PermissionAction.All)
            {
                context.Permissions.Add(new Permission
                {
                    Action = action,
                    Code = $"{module}.{action}",
                    Name = $"{module} - {action}",
                    RolePermissions = [new RolePermission { Role = adminRole }]
                });
            }
        }

        foreach (var (code, name, action) in Shared.Constants.SecurityPermissions.All)
        {
            context.Permissions.Add(new Permission
            {
                Action = action,
                Code = code,
                Name = name,
                RolePermissions = [new RolePermission { Role = adminRole }]
            });
        }

        foreach (var (code, name, action) in Shared.Constants.ConfigPermissions.All)
        {
            context.Permissions.Add(new Permission
            {
                Action = action,
                Code = code,
                Name = name,
                RolePermissions = [new RolePermission { Role = adminRole }]
            });
        }
    }
}
