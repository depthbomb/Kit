using Shared;

namespace Kit.Cli;

internal sealed class StampInputConfiguration
{
    public string? ApplicationName { get; set; }

    public string? InitialVersion { get; set; }

    public string? LaunchExecutable { get; set; }

    public string? LaunchArguments { get; set; }

    public string? BannerImagePath { get; set; }

    public string? WindowIconPath { get; set; }

    public bool? RequiresAppRuntime { get; set; }

    public AppearanceConfiguration? Appearance { get; set; }

    public TextConfiguration? Text { get; set; }

    public InstallationConfiguration? Installation { get; set; }

    public UpdatePolicyConfiguration? UpdatePolicy { get; set; }

    public List<RequiredRuntimeConfiguration>? RequiredRuntimes { get; set; }

    public UpdateSourceConfiguration? UpdateSource { get; set; }
}
