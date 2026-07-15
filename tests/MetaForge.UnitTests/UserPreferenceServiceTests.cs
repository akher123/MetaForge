using MetaForge.Domain.Security;
using MetaForge.Infrastructure.Persistence.Seed;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetaForge.UnitTests;

public class UserPreferenceServiceTests
{
    [Fact]
    public async Task SetThemeAsync_PersistsValidTheme()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User { UserName = "themeuser", Email = "t@x.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.SetThemeAsync(1, AppThemes.OceanLight);

        var user = await context.Users.FindAsync(1);
        Assert.Equal(AppThemes.OceanLight, user!.ThemeKey);
    }

    [Fact]
    public async Task SetThemeAsync_Null_InheritsSystemDefault()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User { UserName = "themeuser", Email = "t@x.com", PasswordHash = "x", ThemeKey = AppThemes.OceanLight });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.SetThemeAsync(1, null);

        var theme = await service.GetThemeAsync(1);
        Assert.Equal(AppThemes.Default, theme);
    }

    [Fact]
    public async Task SetThemeAsync_RejectsUnknownTheme()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User { UserName = "u", Email = "u@x.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await Assert.ThrowsAsync<BusinessException>(() => service.SetThemeAsync(1, "invalid-theme"));
    }

    [Fact]
    public void GetAvailableThemes_ReturnsCatalog()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var themes = service.GetAvailableThemes();
        Assert.Equal(12, themes.Count);
        Assert.Equal(AppThemes.Catalog.Count, themes.Count);
        Assert.Contains(themes, t => t.Key == AppThemes.IndigoDark && t.IsDark);
    }

    private static UserPreferenceService CreateService(MetaForgeDbContext context)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var systemSettings = new SystemSettingsService(context, cache);
        var resolver = new PreferenceResolverService(context, systemSettings);
        return new UserPreferenceService(context, resolver);
    }

    private static async Task<MetaForgeDbContext> CreateSeededContextAsync()
    {
        var context = CreateContext();
        await SystemSettingsSeed.EnsureDefaultsAsync(context, NullLogger.Instance);
        return context;
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
