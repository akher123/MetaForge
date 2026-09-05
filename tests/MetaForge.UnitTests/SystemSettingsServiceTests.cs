using MetaForge.Domain.Security;
using MetaForge.Infrastructure.Persistence.Seed;
using MetaForge.Shared.Constants;
using MetaForge.Shared.Culture;
using MetaForge.Shared.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetaForge.UnitTests;

public class SystemSettingsServiceTests
{
    [Fact]
    public async Task GetLocalizationAsync_ReturnsSeededDefaults()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        var localization = await service.GetLocalizationAsync();

        Assert.True(localization.Enabled);
        Assert.Equal("en-US", localization.DefaultCulture);
        Assert.Equal("en-US", localization.FallbackCulture);
        Assert.Equal(GridDisplayFormats.LocaleDate, localization.DefaultDateFormat);
        Assert.Equal(GridDisplayFormats.LocaleDateTime, localization.DefaultDateTimeFormat);
    }

    [Fact]
    public void GetAvailableCultures_ReturnsDotNetSpecificCultures()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var cultures = service.GetAvailableCultures();

        Assert.NotEmpty(cultures);
        Assert.Contains(cultures, c => c.Name == "en-US");
        Assert.Contains(cultures, c => c.Name == "ar-SA");
        Assert.All(cultures, c => Assert.False(string.IsNullOrWhiteSpace(c.DisplayName)));
    }

    [Fact]
    public async Task UpdateAppearanceAsync_PersistsTheme()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        await service.UpdateAppearanceAsync(new AppearanceSettingsDto
        {
            DefaultThemeKey = AppThemes.OceanLight
        }, updatedByUserId: 1);

        var appearance = await service.GetAppearanceAsync();
        Assert.Equal(AppThemes.OceanLight, appearance.DefaultThemeKey);
    }

    [Fact]
    public async Task UpdateLocalizationAsync_RejectsInvalidCulture()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateLocalizationAsync(new LocalizationSettingsDto
        {
            Enabled = true,
            DefaultCulture = "not-a-real-culture",
            FallbackCulture = "en-US"
        }, updatedByUserId: 1));
    }

    [Fact]
    public async Task UpdateLocalizationAsync_AcceptsValidDotNetCulture()
    {
        await using var context = await CreateSeededContextAsync();
        var service = CreateService(context);

        await service.UpdateLocalizationAsync(new LocalizationSettingsDto
        {
            Enabled = true,
            DefaultCulture = "fr-FR",
            FallbackCulture = "en-US"
        }, updatedByUserId: 1);

        var localization = await service.GetLocalizationAsync();
        Assert.Equal("fr-FR", localization.DefaultCulture);
    }

    private static SystemSettingsService CreateService(MetaForgeDbContext context)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new SystemSettingsService(context, cache);
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

public class DateFormatCatalogTests
{
    [Fact]
    public void GetDateOptions_IncludesMultipleFormatsPerCulture()
    {
        var usOptions = DateFormatCatalog.GetDateOptions("en-US");
        var gbOptions = DateFormatCatalog.GetDateOptions("en-GB");

        Assert.Contains(usOptions, o => o.Key == GridDisplayFormats.LocaleDate);
        Assert.Contains(usOptions, o => o.Key == GridDisplayFormats.DateIso);
        Assert.True(usOptions.Count >= 4);
        Assert.True(gbOptions.Count >= 4);
        Assert.NotEqual(
            usOptions.First(o => o.Key == GridDisplayFormats.LocaleDate).Sample,
            gbOptions.First(o => o.Key == GridDisplayFormats.LocaleDate).Sample);
    }

    [Fact]
    public void IsValidDateFormat_RejectsUnknownKey()
    {
        Assert.False(DateFormatCatalog.IsValidDateFormat("not-a-format", "en-US"));
        Assert.True(DateFormatCatalog.IsValidDateFormat(GridDisplayFormats.DateIso, "en-US"));
    }
}

public class CultureCatalogTests
{
    [Fact]
    public void GetSpecificCultures_IncludesCommonLocales()
    {
        var cultures = CultureCatalog.GetSpecificCultures();
        Assert.Contains(cultures, c => c.Name == "en-US");
        Assert.Contains(cultures, c => c.Name == "bn-BD");
    }

    [Fact]
    public void TryNormalize_AcceptsValidCulture()
    {
        Assert.True(CultureCatalog.TryNormalize("ar-sa", out var normalized));
        Assert.Equal("ar-SA", normalized);
    }

    [Fact]
    public void TryNormalize_RejectsInvalidCulture()
    {
        Assert.False(CultureCatalog.TryNormalize("xx-XX", out _));
    }
}

public class PreferenceResolverServiceTests
{
    [Fact]
    public async Task ResolveAsync_UsesSystemDefaults_WhenUserHasNoOverrides()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User { UserName = "u1", Email = "u1@x.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var resolver = CreateResolver(context);
        var effective = await resolver.ResolveAsync(1);

        Assert.Equal("en-US", effective.Culture);
        Assert.Equal(AppThemes.Default, effective.ThemeKey);
        Assert.Equal(GridDisplayFormats.LocaleDate, effective.DateFormat);
        Assert.Equal(GridDisplayFormats.LocaleDateTime, effective.DateTimeFormat);
        Assert.False(effective.DateFormatIsUserOverride);
        Assert.False(effective.DateTimeFormatIsUserOverride);
        Assert.False(effective.CultureIsUserOverride);
        Assert.False(effective.ThemeIsUserOverride);
    }

    [Fact]
    public async Task ResolveAsync_UsesUserOverrides_WhenPresent()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User
        {
            UserName = "u2",
            Email = "u2@x.com",
            PasswordHash = "x",
            CultureOverride = "ar-SA",
            ThemeKey = AppThemes.MidnightDark
        });
        await context.SaveChangesAsync();

        var resolver = CreateResolver(context);
        var effective = await resolver.ResolveAsync(1);

        Assert.Equal("ar-SA", effective.Culture);
        Assert.Equal(AppThemes.MidnightDark, effective.ThemeKey);
        Assert.True(effective.CultureIsUserOverride);
        Assert.True(effective.ThemeIsUserOverride);
        Assert.True(effective.IsRtl);
    }

    [Fact]
    public async Task ResolveAsync_UsesUserDateFormatOverrides_WhenPresent()
    {
        await using var context = await CreateSeededContextAsync();
        context.Users.Add(new User
        {
            UserName = "u3",
            Email = "u3@x.com",
            PasswordHash = "x",
            DateFormatOverride = GridDisplayFormats.DateIso,
            DateTimeFormatOverride = GridDisplayFormats.DateTimeIso
        });
        await context.SaveChangesAsync();

        var resolver = CreateResolver(context);
        var effective = await resolver.ResolveAsync(1);

        Assert.Equal(GridDisplayFormats.DateIso, effective.DateFormat);
        Assert.Equal(GridDisplayFormats.DateTimeIso, effective.DateTimeFormat);
        Assert.True(effective.DateFormatIsUserOverride);
        Assert.True(effective.DateTimeFormatIsUserOverride);
    }

    private static PreferenceResolverService CreateResolver(MetaForgeDbContext context)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var systemSettings = new SystemSettingsService(context, cache);
        return new PreferenceResolverService(context, systemSettings, cache);
    }

    private static async Task<MetaForgeDbContext> CreateSeededContextAsync()
    {
        var context = new MetaForgeDbContext(new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        await SystemSettingsSeed.EnsureDefaultsAsync(context, NullLogger.Instance);
        return context;
    }
}
