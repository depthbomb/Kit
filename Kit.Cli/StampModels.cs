using Shared;

namespace Kit.Cli;

internal sealed class SetupInputSection
{
    public string? OrganizationName        { get; set; }
    public string? InstallLocation         { get; set; }
    public string? ProcessName             { get; set; }
    public string? AddToPath               { get; set; }
    public bool?   CreateDesktopShortcut   { get; set; }
    public bool?   CreateStartMenuShortcut { get; set; }
    public string? PreInstallCommand       { get; set; }
    public string? PreInstallArguments     { get; set; }
    public string? PostInstallCommand      { get; set; }
    public string? PostInstallArguments    { get; set; }
    public string? WelcomeText             { get; set; }
}

internal sealed class StampInputConfiguration
{
    public string? ApplicationName { get; set; }

    public string? InitialVersion { get; set; }

    public string? LaunchExecutable { get; set; }

    public string? LaunchArguments { get; set; }

    public string? BannerImagePath { get; set; }

    public string? WindowIconPath { get; set; }

    public string? RequiredAppRuntimeVersion { get; set; }

    public AppearanceConfiguration? Appearance { get; set; }

    public TextConfiguration? Text { get; set; }

    public InstallationConfiguration? Installation { get; set; }

    public UpdatePolicyConfiguration? UpdatePolicy { get; set; }

    public List<RequiredRuntimeConfiguration>? RequiredRuntimes { get; set; }

    public UpdateSourceConfiguration? UpdateSource { get; set; }

    // Setup configurations (can be root or nested inside "setup" section)
    public string? OrganizationName        { get; set; }
    public string? InstallLocation         { get; set; }
    public string? ProcessName             { get; set; }
    public string? AddToPath               { get; set; }
    public bool?   CreateDesktopShortcut   { get; set; }
    public bool?   CreateStartMenuShortcut { get; set; }
    public string? PreInstallCommand       { get; set; }
    public string? PreInstallArguments     { get; set; }
    public string? PostInstallCommand      { get; set; }
    public string? PostInstallArguments    { get; set; }
    public string? WelcomeText             { get; set; }

    public SetupInputSection? Setup { get; set; }
}
