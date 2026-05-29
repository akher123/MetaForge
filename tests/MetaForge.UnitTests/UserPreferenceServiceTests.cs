using MetaForge.Domain.Security;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Exceptions;

namespace MetaForge.UnitTests;

public class UserPreferenceServiceTests
{
    [Fact]
    public async Task SetThemeAsync_PersistsValidTheme()
    {
        await using var context = CreateContext();
        context.Users.Add(new User { UserName = "themeuser", Email = "t@x.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var service = new UserPreferenceService(context);
        await service.SetThemeAsync(1, AppThemes.OceanLight);

        var theme = await service.GetThemeAsync(1);
        Assert.Equal(AppThemes.OceanLight, theme);
    }

    [Fact]
    public async Task SetThemeAsync_RejectsUnknownTheme()
    {
        await using var context = CreateContext();
        context.Users.Add(new User { UserName = "u", Email = "u@x.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var service = new UserPreferenceService(context);
        await Assert.ThrowsAsync<BusinessException>(() => service.SetThemeAsync(1, "invalid-theme"));
    }

    [Fact]
    public void GetAvailableThemes_ReturnsCatalog()
    {
        using var context = CreateContext();
        var service = new UserPreferenceService(context);
        var themes = service.GetAvailableThemes();
        Assert.Equal(12, themes.Count);
        Assert.Equal(AppThemes.Catalog.Count, themes.Count);
        Assert.Contains(themes, t => t.Key == AppThemes.IndigoDark && t.IsDark);
    }

    private static MetaForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetaForgeDbContext(options);
    }
}
