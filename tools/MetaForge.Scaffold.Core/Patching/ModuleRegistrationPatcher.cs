namespace MetaForge.Scaffold.Patching;

public static class ModuleRegistrationPatcher
{
    public static bool TryRegisterModule(string registrationPath, Module.ModuleNaming naming, out string? error)
    {
        error = null;
        if (!File.Exists(registrationPath))
        {
            error = $"Module registration file not found: {registrationPath}";
            return false;
        }

        var content = File.ReadAllText(registrationPath);
        if (content.Contains(naming.AddModuleMethodName, StringComparison.Ordinal))
        {
            error = $"{naming.AddModuleMethodName} is already registered.";
            return false;
        }

        var usingStatement = $"using {naming.InfrastructureNamespace};";
        if (!content.Contains(usingStatement, StringComparison.Ordinal))
        {
            const string namespaceMarker = "namespace MetaForge.Web.Modules;";
            var namespaceIndex = content.IndexOf(namespaceMarker, StringComparison.Ordinal);
            if (namespaceIndex < 0)
            {
                error = "Could not find namespace marker in MetaForgeModuleRegistration.cs.";
                return false;
            }

            content = content.Insert(namespaceIndex, usingStatement + Environment.NewLine);
        }

        const string marker = "        services.AddScoped<IModuleDbContextResolver, ModuleDbContextResolver>();";
        var index = content.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            error = "Could not find ModuleDbContextResolver registration marker.";
            return false;
        }

        var call = $"        services.{naming.AddModuleMethodName}(configuration);{Environment.NewLine}";
        content = content.Insert(index, call);
        File.WriteAllText(registrationPath, content);
        return true;
    }
}
