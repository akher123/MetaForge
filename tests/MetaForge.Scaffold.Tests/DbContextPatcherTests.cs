using MetaForge.Scaffold.Patching;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class DbContextPatcherTests
{
    [Fact]
    public void TryPatch_InsertsDbSetBeforeOnModelCreating()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbctx_{Guid.NewGuid():N}.cs");
        var content = """
            public class TestDbContext
            {
                public DbSet<Country> Countries => Set<Country>();

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                }
            }
            """;
        File.WriteAllText(path, content);

        try
        {
            var ok = DbContextPatcher.TryPatch(path, "Warehouse", "Warehouses", entityNamespace: null, out var error);
            Assert.True(ok, error);
            var updated = File.ReadAllText(path);
            Assert.Contains("DbSet<Warehouse> Warehouses", updated);
            Assert.True(updated.IndexOf("DbSet<Warehouse>", StringComparison.Ordinal)
                < updated.IndexOf("OnModelCreating", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryPatch_AddsEntityNamespaceUsingWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbctx_{Guid.NewGuid():N}.cs");
        var content = """
            using Microsoft.EntityFrameworkCore;

            namespace MetaForge.Test.Infrastructure.Persistence;

            public class TestDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                }
            }
            """;
        File.WriteAllText(path, content);

        try
        {
            var ok = DbContextPatcher.TryPatch(
                path,
                "Department",
                "Departments",
                "MetaForge.Test.Domain.Entities",
                out var error);

            Assert.True(ok, error);
            var updated = File.ReadAllText(path);
            Assert.Contains("using MetaForge.Test.Domain.Entities;", updated);
            Assert.Contains("DbSet<Department> Departments", updated);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
