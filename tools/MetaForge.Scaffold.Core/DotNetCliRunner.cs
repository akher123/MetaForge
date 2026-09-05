namespace MetaForge.Scaffold;

public static class DotNetCliRunner
{
    public static async Task<string> RunEfMigrationAddAsync(
        string solutionRoot,
        string solutionFile,
        string infrastructureProject,
        string startupProject,
        string migrationName,
        string outputDir,
        string contextName,
        CancellationToken cancellationToken = default)
    {
        var slnx = Path.Combine(solutionRoot, solutionFile);
        var startup = Path.Combine(solutionRoot, startupProject);
        var infra = Path.Combine(solutionRoot, infrastructureProject);

        var restoreOutput = await RunAsync(
            solutionRoot,
            $"restore \"{slnx}\"",
            "dotnet restore",
            cancellationToken);

        var buildOutput = await RunAsync(
            solutionRoot,
            $"build \"{startup}\" --no-restore /p:SkipStopRunningMetaForgeWeb=true",
            "dotnet build",
            cancellationToken);

        var efOutput = await RunAsync(
            solutionRoot,
            $"ef migrations add {migrationName} --project \"{infra}\" --startup-project \"{startup}\" " +
            $"--output-dir \"{outputDir}\" --context {contextName} -- --property SkipStopRunningMetaForgeWeb=true",
            "dotnet ef migrations add",
            cancellationToken);

        return string.Join(
            Environment.NewLine,
            new[] { restoreOutput, buildOutput, efOutput }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static async Task<string> RunAsync(
        string workingDirectory,
        string arguments,
        string commandLabel,
        CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {commandLabel}.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.Join(
            Environment.NewLine,
            new[] { await stdoutTask, await stderrTask }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{commandLabel} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");

        return output;
    }
}
