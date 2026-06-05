namespace Kit.Cli;

internal sealed class KitRcConfiguration
{
    public KitRcCommandOptions? Stamp { get; set; }

    public KitRcCommandOptions? Inspect { get; set; }

    public KitRcCommandOptions? Manifest { get; set; }

    public KitRcCommandOptions? Release { get; set; }
}

internal sealed class KitRcCommandOptions
{
    public string? Input { get; set; }

    public string? Config { get; set; }

    public string? Output { get; set; }

    public string? AppDir { get; set; }

    public string? Updater { get; set; }

    public string? Package { get; set; }

    public string? Installer { get; set; }

    public string? Version { get; set; }

    public string? OutputDir { get; set; }

    public string? PackageName { get; set; }

    public bool? UpdaterUpdateRequired { get; set; }

    public string? InstallerCommand { get; set; }

    public string? InstallerArgs { get; set; }

    public string? InstallerPath { get; set; }
}

internal sealed class KitRcContext
{
    public KitRcContext(string baseDirectory, KitRcConfiguration configuration)
    {
        BaseDirectory = baseDirectory;
        Configuration = configuration;
    }

    public string BaseDirectory { get; }

    public KitRcConfiguration Configuration { get; }
}
