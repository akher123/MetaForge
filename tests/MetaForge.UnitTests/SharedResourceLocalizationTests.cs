using System.Globalization;
using System.Resources;
using MetaForge.Web.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MetaForge.UnitTests;

public class SharedResourceLocalizationTests
{
    [Theory]
    [InlineData("en-US", "Sign in")]
    [InlineData("fr-FR", "Se connecter")]
    [InlineData("ar-SA", "تسجيل الدخول")]
    [InlineData("de-DE", "Anmelden")]
    [InlineData("es-ES", "Iniciar sesión")]
    [InlineData("bn-BD", "সাইন ইন")]
    public void SharedResource_resolves_ui_strings_for_supported_cultures(string cultureName, string expectedSignIn)
    {
        var resourceManager = new ResourceManager(
            "MetaForge.Web.Resources.SharedResource",
            typeof(SharedResource).Assembly);

        var culture = CultureInfo.GetCultureInfo(cultureName);
        var value = resourceManager.GetString("Auth_SignIn", culture);

        Assert.Equal(expectedSignIn, value);
    }

    [Fact]
    public void StringLocalizer_resolves_default_english_strings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();

        using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<SharedResource>>();

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Confirm Delete", localizer["ConfirmDelete_Title"].Value);
            Assert.Equal("Sign in", localizer["Auth_SignIn"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void GetClientStrings_includes_confirm_delete_keys()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();

        using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<SharedResource>>();
        var strings = MetaForge.Web.Localization.LocalizationServiceCollectionExtensions.GetClientStrings(localizer);

        Assert.Equal("Confirm Delete", strings["confirmDeleteTitle"]);
        Assert.Equal("Yes", strings["yes"]);
        Assert.Equal("No", strings["no"]);
        Assert.Contains("{0}", strings["savedSuccessfully"]);
    }
}
