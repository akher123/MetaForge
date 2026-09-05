using MetaForge.Infrastructure;
using MetaForge.Infrastructure.Persistence.Seed;
using MetaForge.Web.Localization;
using MetaForge.Web.Logging;
using MetaForge.Web.Middleware;
using MetaForge.Web.Modules;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MetaForge web application");

    var builder = WebApplication.CreateBuilder(args);
    builder.ConfigureSerilog();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddMetaForgeModules(builder.Configuration);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var mvcBuilder = builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        })
        .AddMetaForgeLocalization();

    if (builder.Environment.IsDevelopment())
        mvcBuilder.AddRazorRuntimeCompilation();

    builder.Services.AddAuthentication("Cookies")
        .AddCookie("Cookies", options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    if (args.Contains("--reset-db", StringComparer.OrdinalIgnoreCase))
        await DatabaseSeeder.ResetAndSeedAsync(app.Services);
    else
    {
        await MetaForgeModuleRegistration.MigrateAllModulesAsync(app.Services);
        await DatabaseSeeder.SeedAsync(app.Services);
    }

    if (args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase))
    {
        Log.Information("Database seed completed (--seed-only)");
        return;
    }

    app.UseMetaForgeRequestLogging();

    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<SecurityStampValidationMiddleware>();
    app.UseMiddleware<CultureMiddleware>();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "modules",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Landing}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MetaForge web application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
