using System.Text;
using MetaForge.Scaffold;

namespace MetaForge.Scaffold.Module;

public static class ModuleCodeGenerator
{
    public static string GenerateDomainProject(ModuleNaming naming, string solutionRoot)
    {
        var coreDomainRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.DomainProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, ScaffoldConstants.CoreDomainProject));
        var sharedRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.DomainProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, ScaffoldConstants.SharedProject));

        return $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <ItemGroup>
            <ProjectReference Include="{coreDomainRef}" />
            <ProjectReference Include="{sharedRef}" />
          </ItemGroup>

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;
    }

    public static string GenerateApplicationProject(ModuleNaming naming, string solutionRoot)
    {
        var domainRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.ApplicationProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, naming.DomainProjectPath));
        var coreAppRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.ApplicationProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, ScaffoldConstants.CoreApplicationProject));
        var abstractionsRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.ApplicationProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, ScaffoldConstants.ModulesAbstractionsProject));

        return $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <ItemGroup>
            <ProjectReference Include="{domainRef}" />
            <ProjectReference Include="{coreAppRef}" />
            <ProjectReference Include="{abstractionsRef}" />
          </ItemGroup>

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;
    }

    public static string GenerateInfrastructureProject(ModuleNaming naming, string solutionRoot)
    {
        var domainRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.InfrastructureProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, naming.DomainProjectPath));
        var applicationRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.InfrastructureProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, naming.ApplicationProjectPath));
        var abstractionsRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            solutionRoot, naming, naming.InfrastructureProject, ModulePathResolver.ResolveFromSolutionRoot(solutionRoot, ScaffoldConstants.ModulesAbstractionsProject));

        return $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <ItemGroup>
            <ProjectReference Include="{domainRef}" />
            <ProjectReference Include="{applicationRef}" />
            <ProjectReference Include="{abstractionsRef}" />
          </ItemGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8">
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
              <PrivateAssets>all</PrivateAssets>
            </PackageReference>
            <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.8" />
          </ItemGroup>

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;
    }

    public static string GenerateGlobalUsings() =>
        """
        global using MetaForge.Domain.Common;
        """;

    public static string GenerateDbContext(ModuleNaming naming)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine($"namespace {naming.PersistenceNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// EF Core context for the {naming.Name} module (SQL Server schema: {naming.SchemaName}).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {naming.DbContextName} : DbContext");
        sb.AppendLine("{");
        sb.AppendLine($"    public {naming.DbContextName}(DbContextOptions<{naming.DbContextName}> options) : base(options) {{ }}");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        modelBuilder.HasDefaultSchema(\"{naming.SchemaName}\");");
        sb.AppendLine($"        modelBuilder.ApplyConfigurationsFromAssembly(typeof({naming.DbContextName}).Assembly);");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateDependencyInjection(ModuleNaming naming)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine($"using {naming.PersistenceNamespace};");
        sb.AppendLine("using MetaForge.Modules.Abstractions;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {naming.InfrastructureNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {naming.ModuleClassName} : IMetaForgeModule");
        sb.AppendLine("{");
        sb.AppendLine($"    public string Name => \"{naming.Name}\";");
        sb.AppendLine();
        sb.AppendLine($"    public string AreaName => \"{naming.Name}\";");
        sb.AppendLine();
        sb.AppendLine($"    public string SchemaName => \"{naming.SchemaName}\";");
        sb.AppendLine();
        sb.AppendLine($"    public Type DbContextType => typeof({naming.DbContextName});");
        sb.AppendLine();
        sb.AppendLine($"    public Assembly InfrastructureAssembly => typeof({naming.ModuleClassName}).Assembly;");
        sb.AppendLine();
        sb.AppendLine("    public void RegisterServices(IServiceCollection services, IConfiguration configuration)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine("    private const string DefaultConnection =");
        sb.AppendLine("        \"Server=localhost;Database=MetaForgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True\";");
        sb.AppendLine();
        sb.AppendLine($"    public static IServiceCollection {naming.AddModuleMethodName}(this IServiceCollection services, IConfiguration configuration)");
        sb.AppendLine("    {");
        sb.AppendLine("        var connectionString = configuration.GetConnectionString(\"DefaultConnection\") ?? DefaultConnection;");
        sb.AppendLine($"        var migrationsAssembly = typeof({naming.DbContextName}).Assembly.FullName;");
        sb.AppendLine();
        sb.AppendLine($"        services.AddDbContext<{naming.DbContextName}>(options =>");
        sb.AppendLine("            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));");
        sb.AppendLine();
        sb.AppendLine($"        services.AddSingleton<IMetaForgeModule, {naming.ModuleClassName}>();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateHomeController(ModuleNaming naming)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine();
        sb.AppendLine($"namespace MetaForge.Web.Areas.{naming.Name}.Controllers;");
        sb.AppendLine();
        sb.AppendLine($"[Area(\"{naming.Name}\")]");
        sb.AppendLine("[Route(\"[area]/[controller]\")]");
        sb.AppendLine("public class HomeController : Controller");
        sb.AppendLine("{");
        sb.AppendLine("    [HttpGet(\"\")]");
        sb.AppendLine("    public IActionResult Index() => View();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string GenerateIndexView(ModuleNaming naming)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@{");
        sb.AppendLine($"    ViewData[\"Title\"] = \"{naming.Name} Module\";");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("<div class=\"container py-4\">");
        sb.AppendLine($"    <h1>{naming.Name} Module</h1>");
        sb.AppendLine($"    <p class=\"text-muted\">{naming.Name} business module — use Form Builder to configure entity screens, then open them via dynamic modules.</p>");
        sb.AppendLine("    <ul>");
        sb.AppendLine($"        <li><code>/{naming.Name}/Modules/{{EntityName}}</code> — after Form Builder Auto-Build</li>");
        sb.AppendLine("    </ul>");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    public static string GenerateViewStart() =>
        """
        @{
            Layout = "~/Views/Shared/_Layout.cshtml";
        }
        """;
}
