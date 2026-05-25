using MetaForge.Infrastructure;
using MetaForge.Infrastructure.Persistence.Seed;
using MetaForge.Modules.Hr;
using MetaForge.Modules.MasterData;
using MetaForge.Modules.Sales;
using MetaForge.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMasterDataModule();
builder.Services.AddSalesModule();
builder.Services.AddHrModule();
builder.Services.AddMetaForgeWeb(builder.Environment);

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
    await DatabaseSeeder.SeedAsync(app.Services);

if (args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase))
    return;

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMetaForgeWeb();
app.Run();
