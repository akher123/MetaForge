using MetaForge.Application.DTOs;
using MetaForge.Domain.Business;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Repositories;
using MetaForge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MetaForge.UnitTests;

public class DynamicQueryBuilderFilterTests
{
    [Fact]
    public void ApplyFilters_FiltersByEqualsOperator()
    {
        var data = new List<Customer>
        {
            new() { Id = 1, Code = "A", Name = "Alpha", Status = "Active" },
            new() { Id = 2, Code = "B", Name = "Beta", Status = "Inactive" }
        }.AsQueryable();

        var filtered = DynamicQueryBuilder.ApplyFilters(data, new Dictionary<string, string>
        {
            ["Status"] = "Active"
        }).ToList();

        Assert.Single(filtered);
        Assert.Equal("Alpha", filtered[0].Name);
    }

    [Fact]
    public void ApplyFilters_SupportsContainsOperatorSuffix()
    {
        var data = new List<Customer>
        {
            new() { Id = 1, Code = "A", Name = "Contoso Ltd", Status = "Active" },
            new() { Id = 2, Code = "B", Name = "Fabrikam Inc", Status = "Active" }
        }.AsQueryable();

        var filtered = DynamicQueryBuilder.ApplyFilters(data, new Dictionary<string, string>
        {
            ["Name__contains"] = "Contoso"
        }).ToList();

        Assert.Single(filtered);
        Assert.Equal("Contoso Ltd", filtered[0].Name);
    }

    [Fact]
    public void ParseFilterKey_ParsesOperatorSuffix()
    {
        var (property, op) = DynamicQueryBuilder.ParseFilterKey("Amount__gte");
        Assert.Equal("Amount", property);
        Assert.Equal("gte", op);
    }
}

public class ReportConfigurationServiceTests
{
    [Fact]
    public async Task SaveReportAsync_PersistsColumnsAndFilters()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var reportId = await service.SaveReportAsync(new ReportConfigDto
        {
            Code = "customer-list",
            Name = "Customer List",
            EntityName = "Customer",
            GroupName = "Reports",
            ReportType = ReportType.Tabular.ToString(),
            IsActive = true,
            Columns =
            [
                new ReportColumnConfigDto
                {
                    PropertyName = "Code",
                    Label = "Code",
                    ColumnRole = ReportColumnRole.Detail.ToString()
                },
                new ReportColumnConfigDto
                {
                    PropertyName = "Name",
                    Label = "Name",
                    ColumnRole = ReportColumnRole.Detail.ToString()
                }
            ],
            Filters =
            [
                new ReportFilterConfigDto
                {
                    PropertyName = "Status",
                    Label = "Status",
                    Operator = FilterOperator.Equals.ToString(),
                    DefaultValue = "Active"
                }
            ]
        });

        var loaded = await context.ForgeReports
            .Include(r => r.Columns)
            .Include(r => r.Filters)
            .FirstAsync(r => r.Id == reportId);

        Assert.Equal("customer-list", loaded.Code);
        Assert.Equal(2, loaded.Columns.Count);
        Assert.Single(loaded.Filters);
        Assert.Equal("Status", loaded.Filters.First().PropertyName);
    }

    [Fact]
    public async Task SaveReportAsync_GroupedReportRequiresGroupField()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SaveReportAsync(new ReportConfigDto
        {
            Code = "grouped-customers",
            Name = "Grouped Customers",
            EntityName = "Customer",
            ReportType = ReportType.Grouped.ToString(),
            Columns =
            [
                new ReportColumnConfigDto { PropertyName = "Name", Label = "Name" }
            ],
            Groups = []
        }));

        Assert.Contains("group", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveReportAsync_RejectsCalculatedColumnWithoutFormula()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SaveReportAsync(new ReportConfigDto
        {
            Code = "calc-report",
            Name = "Calc Report",
            EntityName = "Customer",
            ReportType = ReportType.Tabular.ToString(),
            Columns =
            [
                new ReportColumnConfigDto
                {
                    PropertyName = "LineTotal",
                    Label = "Line Total",
                    ColumnRole = ReportColumnRole.Calculated.ToString()
                }
            ]
        }));

        Assert.Contains("formula", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ReportConfigurationService CreateService(MetaForgeDbContext context)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new ForgeFormRepository(context),
            new ForgeMenuRepository(context),
            new ForgeReportRepository(context));

        var discovery = new Mock<IEntityMetadataDiscoveryService>();
        discovery.Setup(d => d.Discover(It.IsAny<string>()))
            .Returns(new EntityMetadataDto
            {
                EntityName = "Customer",
                TableName = "Customers",
                Properties =
                [
                    new EntityPropertyMetadataDto { Name = "Id", IsKey = true, ClrType = "System.Int32" },
                    new EntityPropertyMetadataDto { Name = "Code", ClrType = "System.String", IsNullable = false },
                    new EntityPropertyMetadataDto { Name = "Name", ClrType = "System.String", IsNullable = false },
                    new EntityPropertyMetadataDto { Name = "Status", ClrType = "System.String", IsNullable = true }
                ]
            });

        var security = new Mock<ISecurityManagementService>();
        security.Setup(s => s.SyncReportPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var typeResolver = new EntityTypeResolver(context);

        return new ReportConfigurationService(unitOfWork, discovery.Object, security.Object, typeResolver);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
