using System.Text.Json;

namespace Kit.Cli.Commands;

internal static class ManifestCommand
{
    public static int Run(RootCommand command)
    {
        var releaseVersion    = CommandLine.GetRequiredOption(command.Options, "version");
        var updaterPath       = CommandLine.GetRequiredOption(command.Options, "updater");
        var packagePath       = CommandLine.GetRequiredOption(command.Options, "package");
        var installerPath     = CommandLine.GetRequiredOption(command.Options, "installer");
        var fullUpdaterPath   = Path.GetFullPath(updaterPath);
        var fullPackagePath   = Path.GetFullPath(packagePath);
        var fullInstallerPath = Path.GetFullPath(installerPath);
        var outputDirectory = command.Options.TryGetValue("output", out var configuredOutput)
            ? Path.GetFullPath(configuredOutput)
            : Path.GetDirectoryName(fullUpdaterPath) ?? Environment.CurrentDirectory;
        var outputPath = Path.Combine(outputDirectory, "release-manifest.json");

        if (!File.Exists(fullUpdaterPath))
        {
            throw new FileNotFoundException("Updater executable was not found.", fullUpdaterPath);
        }

        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException("Application package was not found.", fullPackagePath);
        }

        if (!File.Exists(fullInstallerPath))
        {
            throw new FileNotFoundException("Updater refresh installer was not found.", fullInstallerPath);
        }

        var updaterUpdateRequired = command.Options.TryGetValue("updater-update-required", out var requiredText)
                                    && ParseBoolean(requiredText, "updater-update-required");

        var manifest = ReleaseManifestBuilder.Build(
            releaseVersion,
            fullUpdaterPath,
            fullPackagePath,
            fullInstallerPath,
            updaterUpdateRequired);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        Console.WriteLine("Release manifest written to:");
        Console.WriteLine(outputPath);

        return 0;
    }

    private static bool ParseBoolean(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Option --" + optionName + " must be either true or false.");
    }
}
