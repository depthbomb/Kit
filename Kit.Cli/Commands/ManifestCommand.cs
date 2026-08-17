using System.Text.Json;

namespace Kit.Cli.Commands;

internal static class ManifestCommand
{
    public static int Run(RootCommand command)
    {
        var releaseVersion = KitRcOptionResolver.GetRequiredValue(command, "version", section => section.Version);
        if (!StampVersion.TryParse(releaseVersion.Value))
        {
            throw new InvalidOperationException("--version must be a valid version string.");
        }

        var updaterPath    = KitRcOptionResolver.GetRequiredPath(command, "updater", section => section.Updater);
        var packagePath    = KitRcOptionResolver.GetRequiredPath(command, "package", section => section.Package);
        var fullUpdaterPath = updaterPath.ResolvePath();
        var fullPackagePath = packagePath.ResolvePath();
        var outputDirectory = KitRcOptionResolver.GetOptionalPath(
                                 command,
                                 "output",
                                 section => section.Output,
                                 new ResolvedOptionValue(Path.GetDirectoryName(fullUpdaterPath) ?? Environment.CurrentDirectory, null))
                             .ResolvePath();
        var outputPath = Path.Combine(outputDirectory, "release-manifest.json");

        if (!File.Exists(fullUpdaterPath))
        {
            throw new FileNotFoundException("Updater executable was not found.", fullUpdaterPath);
        }

        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException("Application package was not found.", fullPackagePath);
        }

        var updaterUpdateRequired = KitRcOptionResolver.GetOptionalBoolean(command, "updater-update-required", section => section.UpdaterUpdateRequired);

        string? fullInstallerPath = null;
        var installerPath = KitRcOptionResolver.GetOptionalValue(command, "installer", section => section.Installer, "");
        if (!string.IsNullOrWhiteSpace(installerPath.Value))
        {
            fullInstallerPath = installerPath.ResolvePath();
            if (!File.Exists(fullInstallerPath))
            {
                throw new FileNotFoundException("Updater refresh installer was not found.", fullInstallerPath);
            }
        }
        else if (updaterUpdateRequired)
        {
            throw new InvalidOperationException("--installer is required when --updater-update-required true.");
        }

        var deltaFromVersion = KitRcOptionResolver.GetOptionalValue(command, "delta-from-version", section => section.DeltaFromVersion, "");
        var deltaPackage = KitRcOptionResolver.GetOptionalValue(command, "delta-package", section => section.DeltaPackage, "");
        var deltaDeleteList = KitRcOptionResolver.GetOptionalValue(command, "delta-delete-list", section => section.DeltaDeleteList, "");
        var fullDeltaPackagePath = string.IsNullOrWhiteSpace(deltaPackage.Value) ? null : deltaPackage.ResolvePath();
        var fullDeltaDeleteListPath = string.IsNullOrWhiteSpace(deltaDeleteList.Value) ? null : deltaDeleteList.ResolvePath();

        var manifest = ReleaseManifestBuilder.Build(
            releaseVersion.Value,
            fullUpdaterPath,
            fullPackagePath,
            fullInstallerPath,
            updaterUpdateRequired,
            deltaFromVersion.Value,
            fullDeltaPackagePath,
            fullDeltaDeleteListPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        Console.WriteLine("Release manifest written to:");
        Console.WriteLine(outputPath);

        return 0;
    }
}
