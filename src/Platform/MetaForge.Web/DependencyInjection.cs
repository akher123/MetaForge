using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MetaForge.Web;

/// <summary>
/// Registers MVC UI services for the MetaForge platform shell.
/// </summary>
public static class DependencyInjection
{
    public static IMvcBuilder AddMetaForgeWeb(this IServiceCollection services, IHostEnvironment environment)
    {
        var mvcBuilder = services.AddControllersWithViews()
            .AddApplicationPart(typeof(DependencyInjection).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        if (environment.IsDevelopment())
            mvcBuilder.AddRazorRuntimeCompilation();

        return mvcBuilder;
    }

    public static WebApplication UseMetaForgeWeb(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.MapStaticAssets();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<Middleware.SecurityStampValidationMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<Middleware.GlobalExceptionMiddleware>();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Landing}/{id?}");

        return app;
    }
}
