using MetaForge.Application.Configuration;
using MetaForge.Shared.Constants;
using MetaForge.Application.Validation;
using MetaForge.Domain.Business;
using MetaForge.Domain.Notifications;
using MetaForge.Domain.Security;
using MetaForge.Infrastructure.Services;
using MetaForge.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        var seedOptions = GetSeedOptions(scope);

        SeedPlatformSecurity(context);

        if (seedOptions.IncludeDemoData)
        {
            SeedBusinessData(context);
            SeedMetadata(context);
            SeedLookups(context);
            SeedDemoFormPermissions(context);
        }
        else
        {
            logger.LogInformation(
                "Demo business data and sample forms skipped (Seed:{Property}=false).",
                nameof(SeedOptions.IncludeDemoData));
        }

        await context.SaveChangesAsync();
        await ApplyDataUpgradesAsync(context, scope, logger);
        logger.LogInformation("Database seeded successfully.");
    }

    private static async Task ApplyDataUpgradesAsync(
        MetaForgeDbContext context,
        IServiceScope scope,
        ILogger logger)
    {
        await ApplyPlatformUpgradesAsync(context, scope, logger);

        if (GetSeedOptions(scope).IncludeDemoData)
            await ApplyDemoUpgradesAsync(context, scope, logger);
        else
            logger.LogInformation("Demo seed upgrades skipped (Seed:{Property}=false).", nameof(SeedOptions.IncludeDemoData));
    }

    /// <summary>
    /// Idempotent platform fixes and framework metadata — safe for production.
    /// </summary>
    private static async Task ApplyPlatformUpgradesAsync(
        MetaForgeDbContext context,
        IServiceScope scope,
        ILogger logger)
    {
        await EnsureUserSecurityStampsAsync(context, logger);
        await UpgradeLegacyPasswordsAsync(context, logger);
        await EnsureSecurityPermissionsAsync(context, logger);
        await EnsureFormPermissionsAsync(context, logger);
        await EnsureEmailDefaultsAsync(context, logger);
        await EnsurePasswordResetEmailTemplateAsync(context, logger);
        await EnsureEmailPermissionsAsync(context, logger);
        await EnsureReportPermissionsAsync(context, logger);
        await SystemSettingsSeed.EnsureDefaultsAsync(context, logger);
        await SystemSettingsSeed.EnsurePermissionsAsync(context, logger);
        await EnsureMenusAsync(scope, logger);
        await EnsureLocationTreeUpgradeAsync(context, logger);
    }

    /// <summary>
    /// Sample ERP demo layout, business data patches, and showcase reports — dev/demo only.
    /// </summary>
    private static async Task ApplyDemoUpgradesAsync(
        MetaForgeDbContext context,
        IServiceScope scope,
        ILogger logger)
    {
        await EnsureCascadeLookupUpgradeAsync(context, logger);
        await EnsureCustomerRegionMultiselectUpgradeAsync(context, logger);
        await EnsurePagedLookupUpgradeAsync(context, logger);
        await EnsureSampleCustomerAsync(context, logger);
        await EnsureSampleTransactionDataAsync(context, logger);
        await EnsureTabbedCustomerUpgradeAsync(context, logger);
        await EnsureTabularSalesOrderUpgradeAsync(context, logger);
        await EnsureSalesOrderAddressFieldAsync(context, logger);
        await EnsureSalesOrderGridActionsAsync(context, logger);
        await EnsureSalesOrderConditionalRulesAsync(context, logger);
        await EnsureSampleReportsAsync(context, logger);
        await EnsureReportExportLayoutAsync(context, logger);
    }

    private static SeedOptions GetSeedOptions(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

    private static async Task EnsureSampleReportsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var added = 0;

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "customer-list"))
        {
            context.ForgeReports.Add(BuildCustomerListReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "salesorder-list"))
        {
            context.ForgeReports.Add(BuildSalesOrderListReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "salesorders-by-status"))
        {
            context.ForgeReports.Add(BuildSalesOrdersByStatusReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "customers-by-status"))
        {
            context.ForgeReports.Add(BuildCustomersByStatusReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "salesorder-items"))
        {
            context.ForgeReports.Add(BuildSalesOrderItemsReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "sales-orders-dynamic"))
        {
            context.ForgeReports.Add(BuildSalesOrdersDynamicReport());
            added++;
        }

        if (!await context.ForgeReports.AnyAsync(r => r.Code == "line-items-by-customer"))
        {
            context.ForgeReports.Add(BuildLineItemsByCustomerReport());
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} sample report(s).", added);
        }

        await UpgradeSalesOrderItemsToCompositeAsync(context, logger);
        await EnsureReportFilterControlsAsync(context, logger);
        await EnsureSampleReportMenusAsync(context, logger);
    }

    private static async Task EnsureReportFilterControlsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var changed = false;

        changed |= await UpgradeReportFiltersAsync(context, "customer-list", f =>
        {
            if (f.PropertyName == "Name")
            {
                f.ControlType = ReportFilterControlType.TextBox;
                f.Operator = FilterOperator.Contains;
            }
            else if (f.PropertyName == "Status")
            {
                f.ControlType = ReportFilterControlType.Dropdown;
                f.Operator = FilterOperator.Equals;
                f.Options = "Active,Inactive";
            }
        });

        changed |= await UpgradeReportFiltersAsync(context, "salesorder-list", f =>
        {
            if (f.PropertyName == "OrderNo")
            {
                f.ControlType = ReportFilterControlType.TextBox;
                f.Operator = FilterOperator.Contains;
            }
            else if (f.PropertyName == "Status")
            {
                f.ControlType = ReportFilterControlType.Dropdown;
                f.Operator = FilterOperator.Equals;
                f.Options = "Draft,Approved,Closed";
            }
            else if (f.PropertyName == "OrderDate")
            {
                f.ControlType = ReportFilterControlType.DateRange;
                f.Operator = FilterOperator.Between;
            }
            else if (f.PropertyName == "CustomerId")
            {
                f.ControlType = ReportFilterControlType.Autocomplete;
                f.Operator = FilterOperator.Equals;
                f.LookupEntity = "Customer";
            }
        });

        changed |= await EnsureSalesOrderListCustomerFilterAsync(context);

        changed |= await UpgradeReportFiltersAsync(context, "sales-orders-dynamic", f =>
        {
            if (f.PropertyName == "Customer.Name")
            {
                f.ControlType = ReportFilterControlType.TextBox;
                f.Operator = FilterOperator.Contains;
            }
            else if (f.PropertyName == "Status")
            {
                f.ControlType = ReportFilterControlType.Dropdown;
                f.Operator = FilterOperator.Equals;
                f.Options = "Draft,Approved,Closed";
            }
            else if (f.PropertyName == "OrderDate")
            {
                f.ControlType = ReportFilterControlType.DateRange;
                f.Operator = FilterOperator.Between;
            }
        });

        changed |= await UpgradeReportFiltersAsync(context, "salesorder-items", f =>
        {
            if (f.PropertyName is "SalesOrder.OrderNo" or "SalesOrder.Customer.Name")
            {
                f.ControlType = ReportFilterControlType.TextBox;
                f.Operator = FilterOperator.Contains;
            }
        });

        changed |= await UpgradeReportFiltersAsync(context, "line-items-by-customer", f =>
        {
            if (f.PropertyName == "SalesOrder.Customer.Name")
            {
                f.ControlType = ReportFilterControlType.TextBox;
                f.Operator = FilterOperator.Contains;
            }
            else if (f.PropertyName == "SalesOrder.OrderDate")
            {
                f.ControlType = ReportFilterControlType.DateRange;
                f.Operator = FilterOperator.Between;
            }
        });

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded sample report filters with TextBox, Dropdown, Autocomplete, and DateRange controls.");
        }
    }

    private static async Task<bool> EnsureSalesOrderListCustomerFilterAsync(MetaForgeDbContext context)
    {
        var report = await context.ForgeReports
            .Include(r => r.Filters)
            .FirstOrDefaultAsync(r => r.Code == "salesorder-list");

        if (report == null || report.Filters.Any(f => f.PropertyName == "CustomerId"))
            return false;

        report.Filters.Add(new ForgeReportFilter
        {
            PropertyName = "CustomerId",
            Label = "Customer",
            Operator = FilterOperator.Equals,
            ControlType = ReportFilterControlType.Autocomplete,
            LookupEntity = "Customer",
            DisplayOrder = report.Filters.Count
        });

        return true;
    }

    private static async Task<bool> UpgradeReportFiltersAsync(
        MetaForgeDbContext context,
        string reportCode,
        Action<ForgeReportFilter> configure)
    {
        var report = await context.ForgeReports
            .Include(r => r.Filters)
            .FirstOrDefaultAsync(r => r.Code == reportCode);

        if (report == null || report.Filters.Count == 0)
            return false;

        var changed = false;
        foreach (var filter in report.Filters)
        {
            var before = $"{filter.ControlType}|{filter.Operator}|{filter.Options}|{filter.LookupEntity}";
            configure(filter);
            if (string.IsNullOrWhiteSpace(filter.ControlType))
                filter.ControlType = ReportFilterControlType.TextBox;
            var after = $"{filter.ControlType}|{filter.Operator}|{filter.Options}|{filter.LookupEntity}";
            if (!string.Equals(before, after, StringComparison.Ordinal))
                changed = true;
        }

        return changed;
    }

    private static async Task UpgradeSalesOrderItemsToCompositeAsync(MetaForgeDbContext context, ILogger logger)
    {
        var report = await context.ForgeReports
            .Include(r => r.Columns)
            .Include(r => r.Filters)
            .FirstOrDefaultAsync(r => r.Code == "salesorder-items");

        if (report == null)
            return;

        if (report.Columns.Any(c => c.PropertyName == "SalesOrder.OrderNo"))
            return;

        report.Description = "Order line items with related order, customer, and product fields resolved dynamically.";
        report.Columns.Clear();
        report.Filters.Clear();

        foreach (var column in BuildSalesOrderItemsReport().Columns)
            report.Columns.Add(column);

        foreach (var filter in BuildSalesOrderItemsReport().Filters)
            report.Filters.Add(filter);

        await context.SaveChangesAsync();
        logger.LogInformation("Upgraded salesorder-items report to dynamic composite columns.");
    }

    private static async Task EnsureReportExportLayoutAsync(MetaForgeDbContext context, ILogger logger)
    {
        var report = await context.ForgeReports
            .Include(r => r.Signatures)
            .FirstOrDefaultAsync(r => r.Code == "customer-list");

        if (report == null || report.Signatures.Count > 0)
            return;

        report.ShowTitleUnderline = true;
        report.ShowSignatureBlock = true;
        report.ExportTitle = "Customer List Report";
        report.HeaderLeft = "MetaForge ERP";
        report.HeaderCenter = "{Title}";
        report.HeaderRight = "{Date}";
        report.FooterLeft = "Confidential";
        report.FooterCenter = string.Empty;
        report.FooterRight = "{DateTime}";
        report.ShowPageNumbers = true;
        report.ShowGeneratedTimestamp = true;
        report.Signatures.Add(new ForgeReportSignature { Label = "Prepared By", DisplayOrder = 0 });
        report.Signatures.Add(new ForgeReportSignature { Label = "Approved By", DisplayOrder = 1 });

        await context.SaveChangesAsync();
        logger.LogInformation("Sample report export layout ensured for customer-list.");
    }

    private static ForgeReport BuildCustomerListReport()
    {
        var report = new ForgeReport
        {
            Code = "customer-list",
            Name = "Customer List",
            EntityName = "Customer",
            GroupName = "Reports",
            ReportType = ReportType.Tabular,
            DisplayOrder = 1,
            IsActive = true,
            Description = "Tabular list of customers with name and status filters."
        };

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "Code", Label = "Customer Code", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Name", Label = "Customer Name", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Email", Label = "Email", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Phone", Label = "Phone", DisplayOrder = 3, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Status", Label = "Status", DisplayOrder = 4, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "CreditLimit", Label = "Credit Limit", DisplayOrder = 5, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = "N2" },
            new ForgeReportColumn { PropertyName = "PaymentTerms", Label = "Payment Terms", DisplayOrder = 6, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None }
        })
        {
            report.Columns.Add(column);
        }

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "Name", Label = "Customer Name", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "Status", Label = "Status", Operator = FilterOperator.Equals, ControlType = ReportFilterControlType.Dropdown, Options = "Active,Inactive", DefaultValue = "Active", DisplayOrder = 1 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildSalesOrderListReport()
    {
        var report = new ForgeReport
        {
            Code = "salesorder-list",
            Name = "Sales Order List",
            EntityName = "SalesOrder",
            GroupName = "Reports",
            ReportType = ReportType.Tabular,
            DisplayOrder = 2,
            IsActive = true,
            Description = "Open sales orders with order number and status filters."
        };

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "OrderNo", Label = "Order No", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "OrderDate", Label = "Order Date", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = GridDisplayFormats.LocaleDate },
            new ForgeReportColumn { PropertyName = "CustomerId", Label = "Customer", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Status", Label = "Status", DisplayOrder = 3, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Address", Label = "Ship To", DisplayOrder = 4, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None }
        })
        {
            report.Columns.Add(column);
        }

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "OrderNo", Label = "Order No", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "CustomerId", Label = "Customer", Operator = FilterOperator.Equals, ControlType = ReportFilterControlType.Autocomplete, LookupEntity = "Customer", DisplayOrder = 1 },
            new ForgeReportFilter { PropertyName = "Status", Label = "Status", Operator = FilterOperator.Equals, ControlType = ReportFilterControlType.Dropdown, Options = "Draft,Approved,Closed", DisplayOrder = 2 },
            new ForgeReportFilter { PropertyName = "OrderDate", Label = "Order Date", Operator = FilterOperator.Between, ControlType = ReportFilterControlType.DateRange, DisplayOrder = 3 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildSalesOrdersByStatusReport()
    {
        var report = new ForgeReport
        {
            Code = "salesorders-by-status",
            Name = "Sales Orders by Status",
            EntityName = "SalesOrder",
            GroupName = "Reports",
            ReportType = ReportType.Grouped,
            DisplayOrder = 3,
            IsActive = true,
            Description = "Sales orders grouped by status with order counts and subtotals."
        };

        report.Groups.Add(new ForgeReportGroup
        {
            PropertyName = "Status",
            Label = "Status",
            DisplayOrder = 0,
            ShowGroupHeader = true,
            ShowSubtotal = true
        });

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "OrderNo", Label = "Order No", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "OrderDate", Label = "Order Date", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = GridDisplayFormats.LocaleDate },
            new ForgeReportColumn { PropertyName = "CustomerId", Label = "Customer", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Id", Label = "Order Count", DisplayOrder = 3, ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Count }
        })
        {
            report.Columns.Add(column);
        }

        report.Summaries.Add(new ForgeReportSummary
        {
            PropertyName = "Id",
            Label = "Total Orders",
            AggregateFunction = ReportAggregateFunction.Count,
            DisplayOrder = 0
        });

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "Status", Label = "Status", Operator = FilterOperator.Equals, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "OrderDate", Label = "From Date", Operator = FilterOperator.GreaterOrEqual, DisplayOrder = 1 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildCustomersByStatusReport()
    {
        var report = new ForgeReport
        {
            Code = "customers-by-status",
            Name = "Customers by Status",
            EntityName = "Customer",
            GroupName = "Reports",
            ReportType = ReportType.Summary,
            DisplayOrder = 4,
            IsActive = true,
            Description = "Summary of customers grouped by status with credit limit totals."
        };

        report.Groups.Add(new ForgeReportGroup
        {
            PropertyName = "Status",
            Label = "Status",
            DisplayOrder = 0,
            ShowGroupHeader = false,
            ShowSubtotal = false
        });

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "Status", Label = "Status", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Id", Label = "Customer Count", DisplayOrder = 1, ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Count },
            new ForgeReportColumn { PropertyName = "CreditLimit", Label = "Total Credit Limit", DisplayOrder = 2, ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Sum, DisplayFormat = "N2" }
        })
        {
            report.Columns.Add(column);
        }

        report.Summaries.Add(new ForgeReportSummary
        {
            PropertyName = "Id",
            Label = "Total Customers",
            AggregateFunction = ReportAggregateFunction.Count,
            DisplayOrder = 0
        });

        report.Summaries.Add(new ForgeReportSummary
        {
            PropertyName = "CreditLimit",
            Label = "Grand Total Credit",
            AggregateFunction = ReportAggregateFunction.Sum,
            DisplayOrder = 1
        });

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "Name", Label = "Customer Name", Operator = FilterOperator.Contains, DisplayOrder = 0 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildSalesOrderItemsReport()
    {
        var report = new ForgeReport
        {
            Code = "salesorder-items",
            Name = "Sales Order Items",
            EntityName = "SalesOrderItem",
            GroupName = "Reports",
            ReportType = ReportType.Tabular,
            DisplayOrder = 5,
            IsActive = true,
            Description = "Order line items with related order, customer, and product fields resolved dynamically."
        };

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "SalesOrder.OrderNo", Label = "Order No", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "SalesOrder.OrderDate", Label = "Order Date", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = GridDisplayFormats.LocaleDate },
            new ForgeReportColumn { PropertyName = "SalesOrder.Customer.Name", Label = "Customer", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Product.Name", Label = "Product", DisplayOrder = 3, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Quantity", Label = "Quantity", DisplayOrder = 4, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "UnitPrice", Label = "Unit Price", DisplayOrder = 5, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = "N2" },
            new ForgeReportColumn { PropertyName = "LineTotal", Label = "Line Total", DisplayOrder = 6, ColumnRole = ReportColumnRole.Calculated, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = "N2", Formula = "{Quantity} * {UnitPrice}" }
        })
        {
            report.Columns.Add(column);
        }

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "SalesOrder.OrderNo", Label = "Order No", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "SalesOrder.Customer.Name", Label = "Customer", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 1 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildSalesOrdersDynamicReport()
    {
        var report = new ForgeReport
        {
            Code = "sales-orders-dynamic",
            Name = "Sales Orders (Dynamic Query)",
            EntityName = "SalesOrder",
            GroupName = "Reports",
            ReportType = ReportType.Tabular,
            DisplayOrder = 6,
            IsActive = true,
            Description = "Sales orders with customer and country fields resolved via dynamic navigation paths (no SQL view)."
        };

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "OrderNo", Label = "Order No", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "OrderDate", Label = "Order Date", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = GridDisplayFormats.LocaleDate },
            new ForgeReportColumn { PropertyName = "Customer.Name", Label = "Customer", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Customer.Email", Label = "Email", DisplayOrder = 3, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Customer.Country.Name", Label = "Country", DisplayOrder = 4, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Status", Label = "Status", DisplayOrder = 5, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Address", Label = "Ship To", DisplayOrder = 6, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None }
        })
        {
            report.Columns.Add(column);
        }

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "Customer.Name", Label = "Customer", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "Status", Label = "Status", Operator = FilterOperator.Equals, ControlType = ReportFilterControlType.Dropdown, Options = "Draft,Approved,Closed", DisplayOrder = 1 },
            new ForgeReportFilter { PropertyName = "OrderDate", Label = "Order Date", Operator = FilterOperator.Between, ControlType = ReportFilterControlType.DateRange, DisplayOrder = 2 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static ForgeReport BuildLineItemsByCustomerReport()
    {
        var report = new ForgeReport
        {
            Code = "line-items-by-customer",
            Name = "Line Items by Customer",
            EntityName = "SalesOrderItem",
            GroupName = "Reports",
            ReportType = ReportType.Grouped,
            DisplayOrder = 7,
            IsActive = true,
            Description = "Order lines grouped by customer using dynamic paths across SalesOrder and Customer."
        };

        report.Groups.Add(new ForgeReportGroup
        {
            PropertyName = "SalesOrder.Customer.Name",
            Label = "Customer",
            DisplayOrder = 0,
            ShowGroupHeader = true,
            ShowSubtotal = true
        });

        foreach (var column in new[]
        {
            new ForgeReportColumn { PropertyName = "SalesOrder.OrderNo", Label = "Order No", DisplayOrder = 0, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Product.Name", Label = "Product", DisplayOrder = 1, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "Quantity", Label = "Quantity", DisplayOrder = 2, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None },
            new ForgeReportColumn { PropertyName = "UnitPrice", Label = "Unit Price", DisplayOrder = 3, ColumnRole = ReportColumnRole.Detail, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = "N2" },
            new ForgeReportColumn { PropertyName = "LineTotal", Label = "Line Total", DisplayOrder = 4, ColumnRole = ReportColumnRole.Calculated, AggregateFunction = ReportAggregateFunction.None, DisplayFormat = "N2", Formula = "{Quantity} * {UnitPrice}" },
            new ForgeReportColumn { PropertyName = "Quantity", Label = "Qty Total", DisplayOrder = 5, ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Sum },
            new ForgeReportColumn { PropertyName = "LineTotal", Label = "Amount Total", DisplayOrder = 6, ColumnRole = ReportColumnRole.Aggregate, AggregateFunction = ReportAggregateFunction.Sum, DisplayFormat = "N2" }
        })
        {
            report.Columns.Add(column);
        }

        report.Summaries.Add(new ForgeReportSummary
        {
            PropertyName = "Quantity",
            Label = "Grand Qty",
            AggregateFunction = ReportAggregateFunction.Sum,
            DisplayOrder = 0
        });

        report.Summaries.Add(new ForgeReportSummary
        {
            PropertyName = "LineTotal",
            Label = "Grand Amount",
            AggregateFunction = ReportAggregateFunction.Sum,
            DisplayOrder = 1
        });

        foreach (var filter in new[]
        {
            new ForgeReportFilter { PropertyName = "SalesOrder.Customer.Name", Label = "Customer", Operator = FilterOperator.Contains, ControlType = ReportFilterControlType.TextBox, DisplayOrder = 0 },
            new ForgeReportFilter { PropertyName = "SalesOrder.OrderDate", Label = "Order Date", Operator = FilterOperator.Between, ControlType = ReportFilterControlType.DateRange, DisplayOrder = 1 }
        })
        {
            report.Filters.Add(filter);
        }

        return report;
    }

    private static async Task EnsureSampleReportMenusAsync(MetaForgeDbContext context, ILogger logger)
    {
        var reportsFolder = await context.ForgeMenus
            .FirstOrDefaultAsync(m => m.ItemType == MenuItemType.Folder && m.Name == "Reports" && m.ParentId == null);

        if (reportsFolder == null)
        {
            reportsFolder = new ForgeMenu
            {
                Name = "Reports",
                ItemType = MenuItemType.Folder,
                Icon = "fa-chart-column",
                DisplayOrder = 3,
                IsActive = true
            };
            context.ForgeMenus.Add(reportsFolder);
            await context.SaveChangesAsync();
        }

        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Customer List", "/Reports/customer-list", 0);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Sales Order List", "/Reports/salesorder-list", 1);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Sales Orders by Status", "/Reports/salesorders-by-status", 2);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Customers by Status", "/Reports/customers-by-status", 3);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Sales Order Items", "/Reports/salesorder-items", 4);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Sales Orders (Dynamic)", "/Reports/sales-orders-dynamic", 5);
        await EnsureReportMenuLinkAsync(context, reportsFolder.Id, "Line Items by Customer", "/Reports/line-items-by-customer", 6);
        await context.SaveChangesAsync();
        logger.LogInformation("Sample report navigation links ensured.");
    }

    private static async Task EnsureReportMenuLinkAsync(
        MetaForgeDbContext context,
        int parentId,
        string name,
        string url,
        int displayOrder)
    {
        if (await context.ForgeMenus.AnyAsync(m => m.ItemType == MenuItemType.Url && m.Url == url))
            return;

        context.ForgeMenus.Add(new ForgeMenu
        {
            ParentId = parentId,
            Name = name,
            ItemType = MenuItemType.Url,
            Url = url,
            Icon = "fa-chart-column",
            DisplayOrder = displayOrder,
            IsActive = true
        });
    }

    private static async Task EnsureReportPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var reports = await context.ForgeReports.Where(r => r.IsActive).AsNoTracking().ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var (code, name, action) in Shared.Constants.ReportConfigPermissions.All)
        {
            if (!permissionByCode.TryGetValue(code, out var permission))
            {
                permission = new Permission { FormId = 0, Action = action, Code = code, Name = name };
                context.Permissions.Add(permission);
                permissionByCode[code] = permission;
                addedPermissions++;
            }

            if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                continue;

            var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
            if (alreadyAssigned)
            {
                adminPermissionIds.Add(permission.Id);
                continue;
            }

            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
            if (permission.Id > 0)
                adminPermissionIds.Add(permission.Id);
            addedAssignments++;
        }

        foreach (var report in reports)
        {
            foreach (var action in Shared.Constants.ReportPermissionAction.All)
            {
                var code = $"{report.Code}.{action}";
                if (!permissionByCode.TryGetValue(code, out var permission))
                {
                    permission = new Permission
                    {
                        FormId = 0,
                        Action = action,
                        Code = code,
                        Name = $"{report.Name} - {action}"
                    };
                    context.Permissions.Add(permission);
                    permissionByCode[code] = permission;
                    addedPermissions++;
                }

                if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                    continue;

                var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                    rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
                if (alreadyAssigned)
                {
                    adminPermissionIds.Add(permission.Id);
                    continue;
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
                "Synced report permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
    }

    private static async Task EnsureEmailDefaultsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var added = 0;

        if (!await context.EmailRetryPolicies.AnyAsync(p => p.Code == "standard"))
        {
            context.EmailRetryPolicies.Add(new EmailRetryPolicy
            {
                Code = "standard",
                Name = "Standard Retry",
                MaxAttempts = 5,
                BackoffStrategy = EmailBackoffStrategy.Exponential,
                BaseDelaySeconds = 60,
                MaxDelaySeconds = 3600,
                BackoffMultiplier = 2.0,
                UseJitter = true,
                IsActive = true,
                IsDefault = true
            });
            added++;
        }

        if (!await context.EmailChannels.AnyAsync(c => c.Code == "default-smtp"))
        {
            context.EmailChannels.Add(new EmailChannel
            {
                Code = "default-smtp",
                Name = "Default SMTP",
                Provider = EmailProviderType.Smtp,
                FromAddress = "noreply@example.com",
                FromDisplayName = "MetaForge",
                SmtpHost = "localhost",
                SmtpPort = 587,
                SmtpUseSsl = true,
                CredentialSecretName = "default-smtp",
                IsActive = true,
                IsDefault = true
            });
            added++;
        }

        if (!await context.EmailChannels.AnyAsync(c => c.Code == "sendgrid"))
        {
            context.EmailChannels.Add(new EmailChannel
            {
                Code = "sendgrid",
                Name = "SendGrid",
                Provider = EmailProviderType.SendGrid,
                FromAddress = "noreply@example.com",
                FromDisplayName = "MetaForge",
                CredentialSecretName = "sendgrid-main",
                IsActive = false,
                IsDefault = false
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} default email configuration record(s).", added);
        }
    }

    private static async Task EnsurePasswordResetEmailTemplateAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (await context.EmailTemplates.AnyAsync(t => t.Code == "password-reset"))
            return;

        var channel = await context.EmailChannels.FirstOrDefaultAsync(c => c.IsDefault && c.IsActive)
            ?? await context.EmailChannels.FirstOrDefaultAsync(c => c.IsActive);
        var policy = await context.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive)
            ?? await context.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsActive);

        context.EmailTemplates.Add(new EmailTemplate
        {
            Code = "password-reset",
            Name = "Password Reset",
            Description = "Sent when a user must set or reset their password.",
            Subject = "Set your {{AppName}} password",
            DefaultToExpression = "{{Email}}",
            BodyHtml = """
                <p>Hello {{UserName}},</p>
                <p>Use the link below to set your password. This link expires in {{ExpiresHours}} hour(s).</p>
                <p><a href="{{ResetLink}}">Set my password</a></p>
                <p>If you did not request this, you can ignore this email.</p>
                """,
            BodyText = """
                Hello {{UserName}},

                Use the link below to set your password. This link expires in {{ExpiresHours}} hour(s).

                {{ResetLink}}

                If you did not request this, you can ignore this email.
                """,
            EmailChannelId = channel?.Id,
            RetryPolicyId = policy?.Id,
            IsActive = true
        });

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded password reset email template.");
    }

    private static async Task EnsureEmailPermissionsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator");
        if (adminRole == null) return;

        var existingPermissions = await context.Permissions.ToListAsync();
        var permissionByCode = existingPermissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync();

        var addedPermissions = 0;
        var addedAssignments = 0;

        foreach (var (code, name, action) in Shared.Constants.EmailConfigPermissions.All)
        {
            if (!permissionByCode.TryGetValue(code, out var permission))
            {
                permission = new Permission { FormId = 0, Action = action, Code = code, Name = name };
                context.Permissions.Add(permission);
                permissionByCode[code] = permission;
                addedPermissions++;
            }

            if (permission.Id > 0 && adminPermissionIds.Contains(permission.Id))
                continue;

            var alreadyAssigned = permission.Id > 0 && await context.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id);
            if (alreadyAssigned)
            {
                adminPermissionIds.Add(permission.Id);
                continue;
            }

            context.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
            if (permission.Id > 0)
                adminPermissionIds.Add(permission.Id);
            addedAssignments++;
        }

        if (addedPermissions > 0 || addedAssignments > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Synced email permissions ({AddedPermissions} new permission(s), {AddedAssignments} admin assignment(s)).",
                addedPermissions,
                addedAssignments);
        }
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
            await menuSync.EnsureSystemAdminMenusAsync();
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

    private static async Task EnsureTabbedCustomerUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        var customerForm = await context.ForgeForms
            .Include(f => f.Fields)
            .Include(f => f.GridColumns)
            .FirstOrDefaultAsync(f => f.Code == "customer");

        if (customerForm == null)
            return;

        var changed = false;

        if (customerForm.FormType != FormType.Tabbed)
        {
            customerForm.FormType = FormType.Tabbed;
            changed = true;
        }

        var fieldOptions = CustomerFieldOptions();
        var displayOrder = customerForm.Fields.Count;

        foreach (var (propertyName, option) in fieldOptions)
        {
            var field = customerForm.Fields.FirstOrDefault(f => f.PropertyName == propertyName);
            if (field == null)
            {
                var seedField = CustomerSeedFields().FirstOrDefault(f => f.Property == propertyName);
                if (string.IsNullOrEmpty(seedField.Property))
                    continue;

                customerForm.Fields.Add(new ForgeField
                {
                    PropertyName = propertyName,
                    Label = option.Label ?? propertyName,
                    ControlType = seedField.Control,
                    IsRequired = seedField.Required,
                    IsVisible = true,
                    DisplayOrder = displayOrder++,
                    ValidationRule = seedField.Validation,
                    LookupEntity = seedField.Lookup,
                    LookupParentField = propertyName == "RegionId" ? "CountryId" : null,
                    SectionName = option.SectionName
                });
                changed = true;
                continue;
            }

            if (option.Label != null && field.Label != option.Label)
            {
                field.Label = option.Label;
                changed = true;
            }

            if (option.SectionName != null && field.SectionName != option.SectionName)
            {
                field.SectionName = option.SectionName;
                changed = true;
            }
        }

        foreach (var columnName in new[] { "Phone", "CreditLimit" })
        {
            if (customerForm.GridColumns.Any(c => c.PropertyName == columnName))
                continue;

            var label = columnName switch
            {
                "Phone" => "Phone",
                "CreditLimit" => "Credit Limit",
                _ => columnName
            };

            customerForm.GridColumns.Add(new ForgeGridColumn
            {
                PropertyName = columnName,
                Label = label,
                DisplayOrder = customerForm.GridColumns.Count,
                IsSortable = true,
                IsSearchable = columnName == "Phone"
            });
            changed = true;
        }

        await BackfillCustomerErpFieldsAsync(context, logger);

        if (!changed)
            return;

        await context.SaveChangesAsync();
        logger.LogInformation("Upgraded Customer form to Tabbed layout with ERP-style sections.");
    }

    private static async Task BackfillCustomerErpFieldsAsync(MetaForgeDbContext context, ILogger logger)
    {
        var customers = await context.Customers.ToListAsync();
        if (customers.Count == 0)
            return;

        var updated = 0;
        foreach (var customer in customers)
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(customer.Phone))
            {
                customer.Phone = customer.Code switch
                {
                    "C001" => "+1 206 555 0100",
                    "C002" => "+1 212 555 0199",
                    _ => "+1 800 555 0100"
                };
                changed = true;
            }

            if (customer.CreditLimit is null or <= 0)
            {
                customer.CreditLimit = customer.Code switch
                {
                    "C001" => 50_000m,
                    "C002" => 25_000m,
                    _ => 10_000m
                };
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(customer.PaymentTerms))
            {
                customer.PaymentTerms = customer.Code switch
                {
                    "C001" => "Net 30",
                    "C002" => "Net 15",
                    _ => "Net 30"
                };
                changed = true;
            }

            if (changed)
                updated++;
        }

        if (updated == 0)
            return;

        await context.SaveChangesAsync();
        logger.LogInformation("Backfilled phone, credit limit, and payment terms on {Count} customer(s).", updated);
    }

    private static (string Property, string Control, bool Required, string? Validation, string? Lookup)[] CustomerSeedFields() =>
    [
        ("Code", ControlType.TextBox, true, null, null),
        ("Name", ControlType.TextBox, true, null, null),
        ("Status", ControlType.TextBox, false, null, null),
        ("Email", ControlType.TextBox, false, "Email", null),
        ("Phone", ControlType.TextBox, false, null, null),
        ("CountryId", ControlType.Dropdown, false, null, "Country"),
        ("RegionId", ControlType.Dropdown, false, null, "Region"),
        ("CreditLimit", ControlType.Number, false, null, null),
        ("PaymentTerms", ControlType.TextBox, false, null, null)
    ];

    private static Dictionary<string, FormFieldOption> CustomerFieldOptions() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = new(Label: "Customer Code", SectionName: "General"),
            ["Name"] = new(Label: "Name", SectionName: "General"),
            ["Status"] = new(Label: "Status", SectionName: "General"),
            ["Email"] = new(Label: "Email", SectionName: "Contacts"),
            ["Phone"] = new(Label: "Phone", SectionName: "Contacts"),
            ["CountryId"] = new(Label: "Country", SectionName: "Location"),
            ["RegionId"] = new(Label: "Region", SectionName: "Location"),
            ["CreditLimit"] = new(Label: "Credit Limit", SectionName: "Accounting"),
            ["PaymentTerms"] = new(Label: "Payment Terms", SectionName: "Accounting")
        };

    private static async Task EnsureSalesOrderAddressFieldAsync(MetaForgeDbContext context, ILogger logger)
    {
        var salesOrderForm = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Code == "salesorder");

        if (salesOrderForm == null)
            return;

        var changed = false;
        var options = SalesOrderFieldOptions();
        var addressField = salesOrderForm.Fields.FirstOrDefault(f =>
            f.PropertyName.Equals("Address", StringComparison.OrdinalIgnoreCase));

        if (addressField == null)
        {
            salesOrderForm.Fields.Add(new ForgeField
            {
                PropertyName = "Address",
                Label = "Ship-To Address",
                ControlType = ControlType.TextArea,
                IsRequired = false,
                IsVisible = true,
                DisplayOrder = salesOrderForm.Fields.Count,
                SectionName = "Shipping"
            });
            changed = true;
        }
        else if (options.TryGetValue("Address", out var addressOption))
        {
            if (addressOption.Label != null && addressField.Label != addressOption.Label)
            {
                addressField.Label = addressOption.Label;
                changed = true;
            }

            if (addressOption.SectionName != null && addressField.SectionName != addressOption.SectionName)
            {
                addressField.SectionName = addressOption.SectionName;
                changed = true;
            }

            if (!string.Equals(addressField.ControlType, ControlType.TextArea, StringComparison.OrdinalIgnoreCase))
            {
                addressField.ControlType = ControlType.TextArea;
                changed = true;
            }

            if (!addressField.IsVisible)
            {
                addressField.IsVisible = true;
                changed = true;
            }
        }

        if (!changed)
            return;

        await context.SaveChangesAsync();
        logger.LogInformation("Ensured Sales Order Address field is configured for form preview and runtime.");
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

    private static async Task EnsureCustomerRegionMultiselectUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        var customerForm = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Code == "customer");

        if (customerForm == null)
            return;

        var changed = false;
        var regionIdsField = customerForm.Fields.FirstOrDefault(f => f.PropertyName == "RegionIds");
        if (regionIdsField == null)
        {
            customerForm.Fields.Add(new ForgeField
            {
                PropertyName = "RegionIds",
                Label = "Regions",
                ControlType = ControlType.MultiSelect,
                IsRequired = false,
                IsVisible = true,
                DisplayOrder = customerForm.Fields.Count,
                LookupEntity = "Region",
                LookupParentField = "CountryId",
                SectionName = "General",
                MappingEntity = "CustomerRegion",
                MappingParentKey = "CustomerId",
                MappingRelatedKey = "RegionId"
            });
            changed = true;
        }
        else
        {
            if (regionIdsField.ControlType != ControlType.MultiSelect)
            {
                regionIdsField.ControlType = ControlType.MultiSelect;
                changed = true;
            }

            regionIdsField.LookupEntity ??= "Region";
            regionIdsField.LookupParentField ??= "CountryId";
            regionIdsField.SectionName ??= "General";
            regionIdsField.MappingEntity ??= "CustomerRegion";
            regionIdsField.MappingParentKey ??= "CustomerId";
            regionIdsField.MappingRelatedKey ??= "RegionId";
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded Customer form with MultiSelect RegionIds mapping field.");
        }

        var sampleCustomer = await context.Customers
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.CountryId != null);

        if (sampleCustomer == null)
            return;

        var regionIds = await context.Regions
            .Where(r => r.CountryId == sampleCustomer.CountryId)
            .Select(r => r.Id)
            .Take(2)
            .ToListAsync();

        if (regionIds.Count == 0)
            return;

        var existing = await context.CustomerRegions
            .Where(cr => cr.CustomerId == sampleCustomer.Id)
            .Select(cr => cr.RegionId)
            .ToListAsync();

        var missing = regionIds.Except(existing).ToList();
        if (missing.Count == 0)
            return;

        foreach (var regionId in missing)
        {
            context.CustomerRegions.Add(new CustomerRegion
            {
                CustomerId = sampleCustomer.Id,
                RegionId = regionId
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded sample CustomerRegion mappings for customer {CustomerId}.", sampleCustomer.Id);
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
        if (!await context.Countries.AnyAsync())
            return;

        await EnsureSampleRegionsAsync(context, logger);

        var countryId = await context.Countries.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
        var regionId = await context.Regions
            .Where(r => r.CountryId == countryId)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();

        var added = 0;

        if (!await context.Customers.AnyAsync(c => c.Code == "C001"))
        {
            context.Customers.Add(
                CreateSampleCustomer("C001", "Contoso Ltd", "info@contoso.com", "+1 206 555 0100",
                    countryId, regionId, 50_000m, "Net 30",
                    new Address { Street = "123 Main St", City = "Seattle", CountryId = countryId }));
            added++;
        }

        if (!await context.Customers.AnyAsync(c => c.Code == "C002"))
        {
            context.Customers.Add(
                CreateSampleCustomer("C002", "Fabrikam Inc", "contact@fabrikam.com", "+1 212 555 0199",
                    countryId, regionId, 25_000m, "Net 15",
                    new Address { Street = "456 Park Ave", City = "New York", CountryId = countryId }));
            added++;
        }

        if (added == 0)
            return;

        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} sample customer(s) for tabbed form and transaction screens.", added);
    }

    private static Customer CreateSampleCustomer(
        string code,
        string name,
        string email,
        string phone,
        int countryId,
        int? regionId,
        decimal creditLimit,
        string paymentTerms,
        Address address) =>
        new()
        {
            Code = code,
            Name = name,
            Email = email,
            Phone = phone,
            Status = "Active",
            CountryId = countryId,
            RegionId = regionId,
            CreditLimit = creditLimit,
            PaymentTerms = paymentTerms,
            Address = address
        };

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

        context.Cities.AddRange(
            new City { Code = "SEA", Name = "Seattle", Region = usWest },
            new City { Code = "LAX", Name = "Los Angeles", Region = usWest },
            new City { Code = "NYC", Name = "New York", Region = usEast },
            new City { Code = "BOS", Name = "Boston", Region = usEast },
            new City { Code = "LON", Name = "London", Region = ukLondon },
            new City { Code = "MUC", Name = "Munich", Region = deBavaria });

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
            Phone = "+1 206 555 0100",
            Status = "Active",
            Country = us,
            Region = usWest,
            CreditLimit = 50_000m,
            PaymentTerms = "Net 30",
            Address = new Address { Street = "123 Main St", City = "Seattle", Country = us }
        };
        context.Customers.Add(customer);

        context.Customers.Add(new Customer
        {
            Code = "C002",
            Name = "Fabrikam Inc",
            Email = "contact@fabrikam.com",
            Phone = "+1 212 555 0199",
            Status = "Active",
            Country = us,
            Region = usEast,
            CreditLimit = 25_000m,
            PaymentTerms = "Net 15",
            Address = new Address { Street = "456 Park Ave", City = "New York", Country = us }
        });

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

            BuildForm("customer", "Customer", "Customer", "Customers", "Master Data", 2, FormType.Tabbed,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("Status", ControlType.TextBox, false, null, null),
                    ("Email", ControlType.TextBox, false, "Email", null),
                    ("Phone", ControlType.TextBox, false, null, null),
                    ("CountryId", ControlType.Dropdown, false, null, "Country"),
                    ("RegionId", ControlType.Dropdown, false, null, "Region"),
                    ("CreditLimit", ControlType.Number, false, null, null),
                    ("PaymentTerms", ControlType.TextBox, false, null, null)
                ],
                grid: ["Code", "Name", "Email", "Phone", "CountryId", "Status", "CreditLimit"],
                cascadeFields: new Dictionary<string, (string CascadeFrom, string? FilterField)>
                {
                    ["RegionId"] = ("CountryId", null)
                },
                fieldOptions: CustomerFieldOptions()),

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
                    ("Status", ControlType.TextBox, false, null, null),
                    ("Address", ControlType.TextArea, false, null, null)
                ],
                fieldOptions: SalesOrderFieldOptions(),
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

        var locationTree = BuildForm("locationtree", "Location Tree", "Country", "Countries", "Master Data", 5, FormType.TreeViewMultiTable,
            fields: [("Code", ControlType.TextBox, true, null, null), ("Name", ControlType.TextBox, true, null, null), ("IsActive", ControlType.Checkbox, false, null, null)],
            grid: ["Code", "Name", "IsActive"]);
        locationTree.TreeLevels = CreateLocationTreeLevels();
        context.ForgeForms.Add(locationTree);

        context.ForgeForms.AddRange(
            BuildForm("region", "Region", "Region", "Regions", "Master Data", 6, FormType.Detail,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("CountryId", ControlType.Dropdown, true, null, "Country")
                ],
                grid: ["Code", "Name", "CountryId"]),

            BuildForm("city", "City", "City", "Cities", "Master Data", 7, FormType.Detail,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("RegionId", ControlType.Dropdown, true, null, "Region")
                ],
                grid: ["Code", "Name", "RegionId"]));
    }

    private static List<ForgeTreeLevel> CreateLocationTreeLevels() =>
    [
        new ForgeTreeLevel
        {
            LevelIndex = 0,
            EntityName = "Country",
            DisplayColumn = "Code, Name",
            DisplayOrder = 0
        },
        new ForgeTreeLevel
        {
            LevelIndex = 1,
            EntityName = "Region",
            ParentEntity = "Country",
            ForeignKey = "CountryId",
            DisplayColumn = "Code, Name",
            DisplayOrder = 1
        },
        new ForgeTreeLevel
        {
            LevelIndex = 2,
            EntityName = "City",
            ParentEntity = "Region",
            ForeignKey = "RegionId",
            DisplayColumn = "Code, Name",
            DisplayOrder = 2
        }
    ];

    private static async Task EnsureLocationTreeUpgradeAsync(MetaForgeDbContext context, ILogger logger)
    {
        await EnsureSampleCitiesAsync(context, logger);

        if (!await context.ForgeForms.AnyAsync(f => f.EntityName == "Region"))
        {
            context.ForgeForms.Add(BuildForm("region", "Region", "Region", "Regions", "Master Data", 6, FormType.Detail,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("CountryId", ControlType.Dropdown, true, null, "Country")
                ],
                grid: ["Code", "Name", "CountryId"]));
            logger.LogInformation("Added Region detail form for location tree.");
        }

        if (!await context.ForgeForms.AnyAsync(f => f.EntityName == "City"))
        {
            context.ForgeForms.Add(BuildForm("city", "City", "City", "Cities", "Master Data", 7, FormType.Detail,
                fields:
                [
                    ("Code", ControlType.TextBox, true, null, null),
                    ("Name", ControlType.TextBox, true, null, null),
                    ("RegionId", ControlType.Dropdown, true, null, "Region")
                ],
                grid: ["Code", "Name", "RegionId"]));
            logger.LogInformation("Added City detail form for location tree.");
        }

        await ApplyLocationTreeDetailDefaultsAsync(context);

        if (!await context.LookupConfigurations.AnyAsync(c => c.EntityName == "City"))
        {
            context.LookupConfigurations.Add(new LookupConfiguration { EntityName = "City", ValueField = "Id", TextField = "Name" });
            await context.SaveChangesAsync();
        }

        var locationTree = await context.ForgeForms
            .Include(f => f.TreeLevels)
            .FirstOrDefaultAsync(f => f.Code == "locationtree");

        if (locationTree == null)
        {
            locationTree = BuildForm("locationtree", "Location Tree", "Country", "Countries", "Master Data", 5, FormType.TreeViewMultiTable,
                fields: [("Code", ControlType.TextBox, true, null, null), ("Name", ControlType.TextBox, true, null, null), ("IsActive", ControlType.Checkbox, false, null, null)],
                grid: ["Code", "Name", "IsActive"]);
            locationTree.TreeLevels = CreateLocationTreeLevels();
            context.ForgeForms.Add(locationTree);
            await context.SaveChangesAsync();
            logger.LogInformation("Added Location Tree multi-table tree screen.");
            return;
        }

        var changed = false;
        if (locationTree.FormType != FormType.TreeViewMultiTable)
        {
            locationTree.FormType = FormType.TreeViewMultiTable;
            changed = true;
        }

        if (locationTree.TreeLevels.Count == 0)
        {
            foreach (var level in CreateLocationTreeLevels())
                locationTree.TreeLevels.Add(level);
            changed = true;
        }
        else
        {
            foreach (var level in locationTree.TreeLevels.Where(l => string.Equals(l.DisplayColumn, "Name", StringComparison.OrdinalIgnoreCase)))
            {
                level.DisplayColumn = "Code, Name";
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Upgraded Location Tree multi-table tree screen.");
        }
    }

    private static async Task ApplyLocationTreeDetailDefaultsAsync(MetaForgeDbContext context)
    {
        var regionForm = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.EntityName == "Region");

        if (regionForm != null)
        {
            var countryField = regionForm.Fields.FirstOrDefault(f => f.PropertyName == "CountryId");
            if (countryField != null)
            {
                countryField.ControlType = ControlType.Hidden;
                countryField.IsVisible = false;
            }
        }

        var cityForm = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.EntityName == "City");

        if (cityForm != null)
        {
            var regionField = cityForm.Fields.FirstOrDefault(f => f.PropertyName == "RegionId");
            if (regionField != null)
            {
                regionField.ControlType = ControlType.Hidden;
                regionField.IsVisible = false;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureSampleCitiesAsync(MetaForgeDbContext context, ILogger logger)
    {
        if (await context.Cities.AnyAsync())
            return;

        var regions = await context.Regions
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        if (regions.Count == 0)
            return;

        var sampleCities = new (string Code, string Name, string RegionCode)[]
        {
            ("SEA", "Seattle", "US-W"),
            ("LAX", "Los Angeles", "US-W"),
            ("NYC", "New York", "US-E"),
            ("BOS", "Boston", "US-E"),
            ("LON", "London", "UK-L"),
            ("MUC", "Munich", "DE-B")
        };

        var added = false;
        foreach (var sample in sampleCities)
        {
            if (!regions.TryGetValue(sample.RegionCode, out var regionId))
                continue;

            context.Cities.Add(new City
            {
                Code = sample.Code,
                Name = sample.Name,
                RegionId = regionId
            });
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Ensured sample cities exist for location tree.");
        }
    }

    private static ForgeForm BuildForm(
        string code, string name, string entityName, string tableName, string group, int order, FormType formType,
        (string Property, string Control, bool Required, string? Validation, string? Lookup)[] fields,
        string[] grid,
        List<ForgeRelation>? relations = null,
        Dictionary<string, (string CascadeFrom, string? FilterField)>? cascadeFields = null,
        Dictionary<string, FormFieldOption>? fieldOptions = null)
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

                FormFieldOption? options = null;
                if (fieldOptions != null)
                    fieldOptions.TryGetValue(f.Property, out options);

                return new ForgeField
                {
                    PropertyName = f.Property,
                    Label = options?.Label ?? f.Lookup ?? f.Property,
                    ControlType = f.Control,
                    IsRequired = f.Required,
                    IsVisible = options?.Visible ?? true,
                    IsReadOnly = options?.ReadOnly ?? false,
                    DisplayOrder = i,
                    ValidationRule = f.Validation ?? (f.Required ? "Required" : null),
                    ConditionalRule = options?.ConditionalRule,
                    LookupEntity = f.Lookup,
                    LookupParentField = cascadeFrom,
                    LookupFilterField = filterField,
                    SectionName = options?.SectionName
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
                    IsSearchable = c is "Code" or "Name" or "Email" or "Phone" or "OrderNo"
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
            new LookupConfiguration { EntityName = "City", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "Customer", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "Product", ValueField = "Id", TextField = "Name" },
            new LookupConfiguration { EntityName = "SalesOrder", ValueField = "Id", TextField = "OrderNo" });
    }

    private static void SeedPlatformSecurity(MetaForgeDbContext context)
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

    private static void SeedDemoFormPermissions(MetaForgeDbContext context)
    {
        var adminRole = context.Roles.Local.FirstOrDefault(r => r.Name == "Administrator");
        if (adminRole == null)
            return;

        foreach (var module in context.ForgeForms.Local.ToList())
        {
            foreach (var action in PermissionAction.All)
            {
                context.Permissions.Add(new Permission
                {
                    Action = action,
                    Code = $"{module.Code}.{action}",
                    Name = $"{module.Name} - {action}",
                    RolePermissions = [new RolePermission { Role = adminRole }]
                });
            }
        }
    }

    private sealed record FormFieldOption(
        string? ConditionalRule = null,
        bool? Visible = null,
        bool? ReadOnly = null,
        string? Label = null,
        string? SectionName = null);

    private static Dictionary<string, FormFieldOption> SalesOrderFieldOptions()
    {
        var disableWhenApproved = SerializeConditionalRules(
            ConditionalRule(ConditionalRuleActions.Disable, "Status", ConditionalRuleOperators.Equal, "Approved"));

        var disableWhenApprovedOrClosed = SerializeConditionalRules(
            ConditionalRule(ConditionalRuleActions.Disable, "Status", ConditionalRuleOperators.Equal, "Approved"),
            ConditionalRule(ConditionalRuleActions.Disable, "Status", ConditionalRuleOperators.Equal, "Closed"));

        return new Dictionary<string, FormFieldOption>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrderNo"] = new FormFieldOption(
                ConditionalRule: disableWhenApprovedOrClosed,
                Label: "Order No",
                SectionName: "Header"),
            ["OrderDate"] = new FormFieldOption(
                ConditionalRule: disableWhenApprovedOrClosed,
                Label: "Order Date",
                SectionName: "Header"),
            ["CustomerId"] = new FormFieldOption(
                ConditionalRule: disableWhenApprovedOrClosed,
                Label: "Customer",
                SectionName: "Header"),
            ["Status"] = new FormFieldOption(
                ConditionalRule: disableWhenApproved,
                Label: "Status",
                SectionName: "Header"),
            ["Address"] = new FormFieldOption(
                Label: "Ship-To Address",
                SectionName: "Shipping")
        };
    }

    private static async Task EnsureSalesOrderConditionalRulesAsync(MetaForgeDbContext context, ILogger logger)
    {
        var salesOrderForm = await context.ForgeForms
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Code == "salesorder");

        if (salesOrderForm == null)
            return;

        var orderNo = salesOrderForm.Fields.FirstOrDefault(f => f.PropertyName == "OrderNo");
        if (orderNo != null && !string.IsNullOrWhiteSpace(orderNo.ConditionalRule))
            return;

        var options = SalesOrderFieldOptions();
        var changed = false;

        foreach (var field in salesOrderForm.Fields)
        {
            if (!options.TryGetValue(field.PropertyName, out var option))
                continue;

            if (!string.IsNullOrWhiteSpace(option.ConditionalRule))
            {
                field.ConditionalRule = option.ConditionalRule;
                changed = true;
            }

            if (option.Label != null && field.Label != option.Label)
            {
                field.Label = option.Label;
                changed = true;
            }

            if (option.SectionName != null && field.SectionName != option.SectionName)
            {
                field.SectionName = option.SectionName;
                changed = true;
            }
        }

        if (!changed)
            return;

        await context.SaveChangesAsync();
        logger.LogInformation("Applied conditional field rules to Sales Order form.");
    }

    private static FieldConditionalRuleDefinition ConditionalRule(
        string action,
        string sourceField,
        string op,
        string? value = null) =>
        new()
        {
            Action = action,
            SourceField = sourceField,
            Operator = op,
            Value = value
        };

    private static string SerializeConditionalRules(params FieldConditionalRuleDefinition[] rules) =>
        FieldConditionalRuleEngine.Serialize(new FieldConditionalRuleSet { Rules = rules.ToList() });
}
