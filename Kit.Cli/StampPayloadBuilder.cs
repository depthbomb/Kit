using Shared;

namespace Kit.Cli;

internal sealed class StampBuildResult
{
    public required UpdaterConfiguration Payload { get; init; }

    public string? ResolvedIconPath { get; init; }
}

internal static class StampPayloadBuilder
{
    public static StampBuildResult Build(StampInputConfiguration configuration, string configDirectory, string? version = null)
    {
        var resolvedBannerPath = ResolveOptionalPath(configuration.BannerImagePath, configDirectory);
        var resolvedIconPath   = ResolveOptionalPath(configuration.WindowIconPath, configDirectory);

        var payload = new UpdaterConfiguration
        {
            ApplicationName    = configuration.ApplicationName?.Trim()  ?? string.Empty,
            InitialVersion     = configuration.InitialVersion?.Trim()   ?? string.Empty,
            UpdaterVersion     = version?.Trim()                        ?? string.Empty,
            LaunchExecutable   = configuration.LaunchExecutable?.Trim() ?? string.Empty,
            LaunchArguments    = configuration.LaunchArguments?.Trim()  ?? string.Empty,
            BannerImageBase64  = resolvedBannerPath == null ? string.Empty : Convert.ToBase64String(File.ReadAllBytes(resolvedBannerPath)),
            WindowIconBase64   = resolvedIconPath   == null ? string.Empty : Convert.ToBase64String(File.ReadAllBytes(resolvedIconPath)),
            RequiresAppRuntime = configuration.RequiresAppRuntime ?? false,
            Appearance         = configuration.Appearance         ?? new AppearanceConfiguration(),
            Text               = configuration.Text               ?? new TextConfiguration(),
            Installation       = configuration.Installation       ?? new InstallationConfiguration(),
            UpdatePolicy       = configuration.UpdatePolicy       ?? new UpdatePolicyConfiguration(),
            RequiredRuntimes = configuration.RequiredRuntimes?.Select(runtime => new RequiredRuntimeConfiguration
            {
                Name    = runtime.Name.Trim(),
                Version = runtime.Version.Trim(),
                Type    = runtime.Type.Trim()
            }).ToList() ?? [],
            UpdateSource = configuration.UpdateSource ?? new UpdateSourceConfiguration()
        };

        return new StampBuildResult
        {
            Payload          = payload,
            ResolvedIconPath = resolvedIconPath
        };
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
