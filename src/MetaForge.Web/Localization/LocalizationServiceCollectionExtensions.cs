using System.Globalization;
using MetaForge.Shared.Culture;
using MetaForge.Web.Resources;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace MetaForge.Web.Localization;

public static class LocalizationServiceCollectionExtensions
{
    public static IMvcBuilder AddMetaForgeLocalization(this IMvcBuilder mvcBuilder)
    {
        mvcBuilder.Services.AddLocalization();

        mvcBuilder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = CultureCatalog.GetSpecificCultures()
                .Select(c => new CultureInfo(c.Name))
                .ToList();

            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.DefaultRequestCulture = new RequestCulture("en-US");
            options.FallBackToParentUICultures = true;
            options.FallBackToParentCultures = true;
        });

        mvcBuilder
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization(options =>
            {
                options.DataAnnotationLocalizerProvider = (_, factory) =>
                    factory.Create(typeof(SharedResource));
            });

        return mvcBuilder;
    }

    public static IReadOnlyDictionary<string, string> GetClientStrings(IStringLocalizer localizer) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["close"] = localizer["Common_Close"].Value,
            ["yes"] = localizer["Common_Yes"].Value,
            ["no"] = localizer["Common_No"].Value,
            ["record"] = localizer["Common_Record"].Value,
            ["confirmDeleteTitle"] = localizer["ConfirmDelete_Title"].Value,
            ["confirmDeleteMessage"] = localizer["ConfirmDelete_Message"].Value,
            ["confirmDeleteDetail"] = localizer["ConfirmDelete_Detail"].Value,
            ["deleteThisItem"] = localizer["Ui_DeleteThisItem"].Value,
            ["savedSuccessfully"] = localizer["Ui_SavedSuccessfully"].Value,
            ["cultureCustom"] = localizer["Culture_Custom"].Value,
            ["cultureSystemDefault"] = localizer["Culture_SystemDefault"].Value,
            ["cultureSaving"] = localizer["Culture_Saving"].Value,
            ["cultureUseSystemDefault"] = localizer["Culture_UseSystemDefaultShort"].Value
        };
}
