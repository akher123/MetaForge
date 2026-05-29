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
            var ok = DbContextPatcher.TryPatch(path, "Warehouse", "Warehouses", out var error);
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
}
