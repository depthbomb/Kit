// ReSharper disable once CheckNamespace

namespace Shared;

internal sealed class UpdaterConfiguration
{
    public string ApplicationName { get; set; } = string.Empty;

    public string InitialVersion { get; set; } = string.Empty;

    /// <summary>
    /// The application release version that this bootstrapper was stamped for.
    /// Set automatically by the Stamper's release command and used to determine
    /// whether a self-update installer has already been applied, preventing
    /// the bootstrapper from downloading and running the installer again on the
    /// next launch after an updater update.
    /// </summary>
    public string UpdaterVersion { get; set; } = string.Empty;

    public string LaunchExecutable { get; set; } = string.Empty;

    public string LaunchArguments { get; set; } = string.Empty;

    public string BannerImageBase64 { get; set; } = string.Empty;

    public string WindowIconBase64 { get; set; } = string.Empty;

    public string RequiredAppRuntimeVersion { get; set; } = string.Empty;

    public bool RequireWebView2Runtime { get; set; } = false;

    public AppearanceConfiguration Appearance { get; set; } = new();

    public TextConfiguration Text { get; set; } = new();

    public InstallationConfiguration Installation { get; set; } = new();

    public UpdatePolicyConfiguration UpdatePolicy { get; set; } = new();

    public List<RequiredRuntimeConfiguration> RequiredRuntimes { get; set; } = new();

    public UpdateSourceConfiguration UpdateSource { get; set; } = new();
}

internal sealed class UpdateSourceConfiguration
{
    public string Type { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public bool IncludePrerelease { get; set; }
}

internal sealed class InstallationConfiguration
{
    public bool RequireIntegrityVerification { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public int KeepLastVersions { get; set; }

    public string ExtractionLayout { get; set; } = string.Empty;

    public string PostInstallCommand { get; set; } = string.Empty;

    public string PostInstallArguments { get; set; } = string.Empty;

    public bool AllowFreshInstall { get; set; }

    public bool CompressFiles { get; set; } = true;
}

internal sealed class UpdatePolicyConfiguration
{
    public string Mode { get; set; } = string.Empty;

    public string MinimumVersion { get; set; } = string.Empty;
}

internal sealed class AppearanceConfiguration
{
    public bool UseDarkMode { get; set; }

    public bool UseDarkTitleBar { get; set; }

    public string BackgroundColor { get; set; } = string.Empty;

    public string ForegroundColor { get; set; } = string.Empty;

    public string SecondaryTextColor { get; set; } = string.Empty;

    public string BannerBackgroundColor { get; set; } = string.Empty;
}

internal sealed class TextConfiguration
{
    public string WindowTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string InitialStatus { get; set; } = string.Empty;

    public string LoadingConfigurationStatus { get; set; } = string.Empty;

    public string ResolvingInstallationStatus { get; set; } = string.Empty;

    public string CheckingForUpdatesStatus { get; set; } = string.Empty;

    public string NoUpdatesLaunchingStatus { get; set; } = string.Empty;

    public string UpdateAlreadyDownloadedStatus { get; set; } = string.Empty;

    public string UpdatePromptBody { get; set; } = string.Empty;

    public string UpdatePromptTitle { get; set; } = string.Empty;

    public string LaunchingCurrentVersionStatus { get; set; } = string.Empty;

    public string DownloadingVersionStatus { get; set; } = string.Empty;

    public string DownloadingProgressStatus { get; set; } = string.Empty;

    public string LaunchingUpdatedVersionStatus { get; set; } = string.Empty;

    public string ErrorDialogTitle { get; set; } = string.Empty;

    public string ReleaseNotesLabel { get; set; } = string.Empty;

    public string DownloadUpdateButtonText { get; set; } = string.Empty;

    public string SkipForSessionButtonText { get; set; } = string.Empty;

    public string LaunchCurrentVersionButtonText { get; set; } = string.Empty;

    public string SkipVersionButtonText { get; set; } = string.Empty;

    public string CancelButtonText { get; set; } = string.Empty;

    public string VerifyingIntegrityStatus { get; set; } = string.Empty;

    public string ExtractingArchiveStatus { get; set; } = string.Empty;

    public string CompressingFilesStatus { get; set; } = string.Empty;

    public string PreparingFilesStatus { get; set; } = string.Empty;

    public string ValidatingInstallationStatus { get; set; } = string.Empty;

    public string RunningPostInstallStatus { get; set; } = string.Empty;

    public string FinalizingInstallationStatus { get; set; } = string.Empty;

    public string CleaningUpStatus { get; set; } = string.Empty;

    public string ApplicationAlreadyRunningStatus { get; set; } = string.Empty;

    public string ApplicationAlreadyRunningDialogTitle { get; set; } = string.Empty;

    public string ApplicationAlreadyRunningDialogBody { get; set; } = string.Empty;

    public string CheckingRuntimesStatus { get; set; } = string.Empty;

    public string InstallingRuntimeStatus { get; set; } = string.Empty;

    public string DownloadingRuntimeProgressStatus { get; set; } = string.Empty;

    public string RuntimeRequirementPromptTitle { get; set; } = string.Empty;

    public string RuntimeRequirementPromptBody { get; set; } = string.Empty;

    public string CheckingAppRuntimesStatus { get; set; } = string.Empty;

    public string InstallingAppRuntimeStatus { get; set; } = string.Empty;

    public string DownloadingAppRuntimeProgressStatus { get; set; } = string.Empty;

    public string AppRuntimeRequirementPromptTitle { get; set; } = string.Empty;

    public string AppRuntimeRequirementPromptBody { get; set; } = string.Empty;
}

internal sealed class RequiredRuntimeConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
}

internal sealed class ReleaseManifest
{
    public string ApplicationName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool UpdaterUpdateRequired { get; set; }

    public ReleaseDownloadInstruction Download { get; set; } = new();

    public ReleasePackageReference ApplicationPackage { get; set; } = new();

    public ReleasePackageReference UpdaterPackage { get; set; } = new();
}

internal sealed class ReleaseDownloadInstruction
{
    public string Kind { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}

internal sealed class ReleasePackageReference
{
    public string Kind { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}
