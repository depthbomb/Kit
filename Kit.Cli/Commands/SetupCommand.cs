using Shared;
using System.Text.Json;

namespace Kit.Cli.Commands;

internal static class SetupCommand
{
    public static int Run(RootCommand command)
    {
        var inputPath   = CommandLine.GetRequiredOption(command.Options, "input");
        var packagePath = CommandLine.GetRequiredOption(command.Options, "package");
        var configPath  = CommandLine.GetRequiredOption(command.Options, "config");
        var outputPath  = command.Options.GetValueOrDefault("output", inputPath);

        var fullInputPath   = Path.GetFullPath(inputPath);
        var fullPackagePath = Path.GetFullPath(packagePath);
        var fullConfigPath  = Path.GetFullPath(configPath);
        var fullOutputPath  = Path.GetFullPath(outputPath);

        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("Input setup binary was not found.", fullInputPath);
        }

        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException("Application package ZIP file was not found.", fullPackagePath);
        }

        if (!File.Exists(fullConfigPath))
        {
            throw new FileNotFoundException("Stamp configuration file was not found.", fullConfigPath);
        }

        var configDirectory    = Path.GetDirectoryName(fullConfigPath) ?? Environment.CurrentDirectory;
        var stampConfiguration = StampConfigurationLoader.Load(fullConfigPath);

        Console.WriteLine($"Reading application package ZIP: '{fullPackagePath}'...");
        var packageBytes = File.ReadAllBytes(fullPackagePath);

        var resolvedIconPath   = ResolveOptionalPath(stampConfiguration.WindowIconPath, configDirectory);
        var resolvedBannerPath = ResolveOptionalPath(stampConfiguration.BannerImagePath, configDirectory);

        string? windowIconBase64 = null;
        if (resolvedIconPath != null)
        {
            windowIconBase64 = Convert.ToBase64String(File.ReadAllBytes(resolvedIconPath));
        }

        string? bannerImageBase64 = null;
        if (resolvedBannerPath != null)
        {
            bannerImageBase64 = Convert.ToBase64String(File.ReadAllBytes(resolvedBannerPath));
        }

        var setupConfig = new SetupConfiguration
        {
            ApplicationName         = stampConfiguration.ApplicationName                ?? string.Empty,
            OrganizationName        = stampConfiguration.Setup?.OrganizationName        ?? stampConfiguration.OrganizationName ?? string.Empty,
            InstallLocation         = stampConfiguration.Setup?.InstallLocation         ?? stampConfiguration.InstallLocation  ?? "%LOCALAPPDATA%",
            ProcessName             = stampConfiguration.Setup?.ProcessName             ?? stampConfiguration.ProcessName      ?? stampConfiguration.Installation?.ProcessName ?? string.Empty,
            AddToPath               = stampConfiguration.Setup?.AddToPath               ?? stampConfiguration.AddToPath        ?? string.Empty,
            LaunchExecutable        = stampConfiguration.LaunchExecutable               ?? string.Empty,
            LaunchArguments         = stampConfiguration.LaunchArguments                ?? string.Empty,
            CreateDesktopShortcut   = stampConfiguration.Setup?.CreateDesktopShortcut   ?? stampConfiguration.CreateDesktopShortcut   ?? true,
            CreateStartMenuShortcut = stampConfiguration.Setup?.CreateStartMenuShortcut ?? stampConfiguration.CreateStartMenuShortcut ?? true,
            PreInstallCommand       = stampConfiguration.Setup?.PreInstallCommand       ?? stampConfiguration.PreInstallCommand       ?? string.Empty,
            PreInstallArguments     = stampConfiguration.Setup?.PreInstallArguments     ?? stampConfiguration.PreInstallArguments     ?? string.Empty,
            PostInstallCommand      = stampConfiguration.Setup?.PostInstallCommand      ?? stampConfiguration.PostInstallCommand      ?? stampConfiguration.Installation?.PostInstallCommand   ?? string.Empty,
            PostInstallArguments    = stampConfiguration.Setup?.PostInstallArguments    ?? stampConfiguration.PostInstallArguments    ?? stampConfiguration.Installation?.PostInstallArguments ?? string.Empty,
            WelcomeText             = stampConfiguration.Setup?.WelcomeText             ?? stampConfiguration.WelcomeText             ?? "This wizard will guide you through the installation of {ApplicationName}.",
            WindowIconBase64        = windowIconBase64                                  ?? string.Empty,
            BannerImageBase64       = bannerImageBase64                                 ?? string.Empty,
            PackageZipBase64        = Convert.ToBase64String(packageBytes)
        };

        var payloadJson = JsonSerializer.Serialize(setupConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Console.WriteLine($"Stamping setup installer to '{fullOutputPath}'...");
        StampedUpdaterWriter.Write(fullInputPath, fullOutputPath, payloadJson, resolvedIconPath);

        Console.WriteLine("Stamped setup installer written successfully!");
        Console.WriteLine(fullOutputPath);

        return 0;
    }

    private static string? ResolveOptionalPath(string? path, string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidate = path.Trim();
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.Combine(configDirectory, candidate);
        }

        return Path.GetFullPath(candidate);
    }
}
