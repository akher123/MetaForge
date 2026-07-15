namespace MetaForge.Web.Theme;

internal static class CultureCookie
{
    public const string Name = "metaforge_culture";

    public static CookieOptions Options(HttpContext httpContext) => new()
    {
        HttpOnly = false,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = httpContext.Request.IsHttps,
        MaxAge = TimeSpan.FromDays(365),
        Path = "/"
    };
}
