using Shared;
using System.Runtime.InteropServices;

namespace Kit.Updater.Forms;

internal sealed partial class MainForm : Form, IUpdaterView
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private UpdaterConfiguration _configuration;

    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode           = 20;

    private readonly CancellationTokenSource _cts;

    public MainForm()
    {
        InitializeComponent();

        _cts           = new CancellationTokenSource();
        _configuration = new UpdaterConfiguration();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        _ = RunWorkflowAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();

        base.OnFormClosing(e);
    }

    private Task RunWorkflowAsync()
    {
        var workflow = new UpdaterWorkflow();
        return workflow.RunAsync(this, _cts.Token);
    }

    public void ApplyConfiguration(UpdaterConfiguration configuration)
    {
        _configuration = configuration;

        Text               = GetText("WindowTitle", "{ApplicationName} Bootstrapper");
        c_TitleLabel.Text  = GetText("Title", "{ApplicationName}");
        c_StatusLabel.Text = GetText("InitialStatus", "Starting...");

        ApplyAppearance(configuration.Appearance);
        TryLoadWindowIcon(configuration.WindowIconBase64);
        TryLoadBanner(configuration.BannerImageBase64);
    }

    public void SetStatus(string key, string fallback, bool indeterminate, string? version = null, int? percent = null, string? processName = null, string? runtimeNames = null, string? runtimeName = null)
    {
        SetStatusMessage(GetText(key, fallback, version, percent, processName, runtimeNames, runtimeName), indeterminate);
    }

    public bool ConfirmRuntimeInstallation(IReadOnlyList<RequiredRuntimeConfiguration> missingRuntimes)
    {
        var runtimeNames = string.Join(", ", missingRuntimes.Select(r => $"{r.Name} {r.Version}"));
        var result = MessageBox.Show(
            this,
            GetText("RuntimeRequirementPromptBody", "This application requires the following .NET runtimes:\r\n\r\n{RuntimeNames}\r\n\r\nWould you like to download and install them now?", runtimeNames: runtimeNames),
            GetText("RuntimeRequirementPromptTitle", ".NET Runtime Required"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }

    public bool ConfirmAppRuntimeInstallation()
    {
        var result = MessageBox.Show(
            this,
            GetText("AppRuntimeRequirementPromptBody", "This application requires the Windows App Runtime to be installed.\r\n\r\nWould you like to download and install it now?"),
            GetText("AppRuntimeRequirementPromptTitle", "Windows App Runtime Required"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }

    public UpdatePromptChoice PromptForUpdate(AvailableUpdate update, bool allowLaunchCurrent, bool allowSkipVersion)
    {
        using (var prompt = new UpdatePromptForm(new UpdatePromptRequest
               {
                   WindowTitle             = GetText("UpdatePromptTitle", "{ApplicationName} Update Available", update.DisplayVersion),
                   Message                 = GetText("UpdatePromptBody", "A newer version of {ApplicationName} is available ({Version}).", update.DisplayVersion),
                   DownloadButtonText      = GetText("DownloadUpdateButtonText", "Download update"),
                   LaunchCurrentButtonText = GetText("LaunchCurrentVersionButtonText", "Launch current"),
                   SkipVersionButtonText   = GetText("SkipVersionButtonText", "Skip this version"),
                   CancelButtonText        = GetText("CancelButtonText", "Cancel"),
                   AllowLaunchCurrent      = allowLaunchCurrent,
                   AllowSkipVersion        = allowSkipVersion
               }))
        {
            var dialogResult = prompt.ShowDialog(this);

            return dialogResult == DialogResult.OK ? prompt.Choice : UpdatePromptChoice.Cancel;
        }
    }

    public bool ConfirmApplicationClosedForInstall(string version, string processName)
    {
        var answer = MessageBox.Show(
            this,
            GetText("ApplicationAlreadyRunningDialogBody", "{ApplicationName} is currently running. Close it before installing version {Version}, then choose Retry. Choose Cancel to stop the update.", version, processName: processName),
            GetText("ApplicationAlreadyRunningDialogTitle", "{ApplicationName} is running"),
            MessageBoxButtons.RetryCancel,
            MessageBoxIcon.Warning);
        return answer == DialogResult.Retry;
    }

    public void ReportProgress(InstallationProgress progress)
    {
        UpdateInstallationProgress(progress);
    }

    public void ShowError(string message, string titleFallback)
    {
        MessageBox.Show(this, message, GetText("ErrorDialogTitle", titleFallback), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public void CloseWindow()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(Close));
            return;
        }

        Close();
    }

    private void UpdateInstallationProgress(InstallationProgress progress)
    {
        switch (progress.Phase)
        {
            case InstallationPhase.Downloading:
                if (progress.TotalBytes is > 0)
                {
                    var percentage = (int)Math.Max(0, Math.Min(100, progress.BytesReceived.GetValueOrDefault() * 100 / progress.TotalBytes.Value));
                    if (c_ProgressBar.Style != ProgressBarStyle.Continuous)
                    {
                        c_ProgressBar.Style = ProgressBarStyle.Continuous;
                    }

                    c_ProgressBar.Value = percentage;
                    c_StatusLabel.Text  = GetText("DownloadingProgressStatus", "Downloading update... {Percent}%", progress.Version, percentage);
                }
                else
                {
                    SetStatusMessage(GetText("DownloadingVersionStatus", "Downloading version {Version}...", progress.Version), true);
                }
                break;
            case InstallationPhase.VerifyingIntegrity:
                SetStatusMessage(GetText("VerifyingIntegrityStatus", "Verifying download..."), true);
                break;
            case InstallationPhase.ExtractingArchive:
                SetStatusMessage(GetText("ExtractingArchiveStatus", "Extracting archive..."), true);
                break;
            case InstallationPhase.CompressingFiles:
                SetStatusMessage(GetText("CompressingFilesStatus", "Compressing files..."), true);
                break;
            case InstallationPhase.PreparingFiles:
                SetStatusMessage(GetText("PreparingFilesStatus", "Preparing files..."), true);
                break;
            case InstallationPhase.ValidatingInstallation:
                SetStatusMessage(GetText("ValidatingInstallationStatus", "Validating installation..."), true);
                break;
            case InstallationPhase.RunningPostInstall:
                SetStatusMessage(GetText("RunningPostInstallStatus", "Running post-install steps..."), true);
                break;
            case InstallationPhase.FinalizingInstallation:
                SetStatusMessage(GetText("FinalizingInstallationStatus", "Finalizing installation..."), true);
                break;
            case InstallationPhase.CleaningUp:
                SetStatusMessage(GetText("CleaningUpStatus", "Cleaning up temporary files..."), true);
                break;
            case InstallationPhase.CheckingRuntimes:
                SetStatusMessage(GetText("CheckingRuntimesStatus", "Checking for required .NET runtimes..."), true);
                break;
            case InstallationPhase.DownloadingRuntime:
                if (progress.TotalBytes is > 0)
                {
                    var percentage = (int)Math.Max(0, Math.Min(100, progress.BytesReceived.GetValueOrDefault() * 100 / progress.TotalBytes.Value));
                    if (c_ProgressBar.Style != ProgressBarStyle.Continuous)
                    {
                        c_ProgressBar.Style = ProgressBarStyle.Continuous;
                    }

                    c_ProgressBar.Value = percentage;
                    c_StatusLabel.Text  = GetText("DownloadingRuntimeProgressStatus", "Downloading .NET runtime... {Percent}%", progress.Version, percentage);
                }
                else
                {
                    SetStatusMessage(GetText("InstallingRuntimeStatus", "Installing runtime version {Version}...", progress.Version), true);
                }
                break;
            case InstallationPhase.InstallingRuntime:
                SetStatusMessage(GetText("InstallingRuntimeStatus", "Installing runtime version {Version}...", progress.Version), true);
                break;
            case InstallationPhase.DownloadingAppRuntime:
                if (progress.TotalBytes is > 0)
                {
                    var percentage = (int)Math.Max(0, Math.Min(100, progress.BytesReceived.GetValueOrDefault() * 100 / progress.TotalBytes.Value));
                    if (c_ProgressBar.Style != ProgressBarStyle.Continuous)
                    {
                        c_ProgressBar.Style = ProgressBarStyle.Continuous;
                    }

                    c_ProgressBar.Value = percentage;
                    c_StatusLabel.Text  = GetText("DownloadingAppRuntimeProgressStatus", "Downloading Windows App Runtime... {Percent}%", percent: percentage);
                }
                else
                {
                    SetStatusMessage(GetText("InstallingAppRuntimeStatus", "Installing Windows App Runtime..."), true);
                }
                break;
            case InstallationPhase.InstallingAppRuntime:
                SetStatusMessage(GetText("InstallingAppRuntimeStatus", "Installing Windows App Runtime..."), true);
                break;
        }
    }

    private void SetStatusMessage(string message, bool indeterminate)
    {
        c_StatusLabel.Text = message;
        if (indeterminate)
        {
            if (c_ProgressBar.Style != ProgressBarStyle.Marquee)
            {
                c_ProgressBar.Style = ProgressBarStyle.Marquee;
            }
        }
        else
        {
            if (c_ProgressBar.Style != ProgressBarStyle.Continuous)
            {
                c_ProgressBar.Style = ProgressBarStyle.Continuous;
                c_ProgressBar.Value = 0;
            }
        }
    }

    private string GetText(string key, string fallback, string? version = null, int? percent = null, string? processName = null, string? runtimeNames = null, string? runtimeName = null)
    {
        var text = _configuration.Text;
        var template = key switch
        {
            "CheckingRuntimesStatus"               => string.IsNullOrWhiteSpace(text.CheckingRuntimesStatus) ? fallback : text.CheckingRuntimesStatus,
            "InstallingRuntimeStatus"              => string.IsNullOrWhiteSpace(text.InstallingRuntimeStatus) ? fallback : text.InstallingRuntimeStatus,
            "DownloadingRuntimeProgressStatus"     => string.IsNullOrWhiteSpace(text.DownloadingRuntimeProgressStatus) ? fallback : text.DownloadingRuntimeProgressStatus,
            "RuntimeRequirementPromptTitle"        => string.IsNullOrWhiteSpace(text.RuntimeRequirementPromptTitle) ? fallback : text.RuntimeRequirementPromptTitle,
            "RuntimeRequirementPromptBody"         => string.IsNullOrWhiteSpace(text.RuntimeRequirementPromptBody) ? fallback : text.RuntimeRequirementPromptBody,
            "CheckingAppRuntimesStatus"            => string.IsNullOrWhiteSpace(text.CheckingAppRuntimesStatus) ? fallback : text.CheckingAppRuntimesStatus,
            "InstallingAppRuntimeStatus"           => string.IsNullOrWhiteSpace(text.InstallingAppRuntimeStatus) ? fallback : text.InstallingAppRuntimeStatus,
            "DownloadingAppRuntimeProgressStatus"  => string.IsNullOrWhiteSpace(text.DownloadingAppRuntimeProgressStatus) ? fallback : text.DownloadingAppRuntimeProgressStatus,
            "AppRuntimeRequirementPromptTitle"     => string.IsNullOrWhiteSpace(text.AppRuntimeRequirementPromptTitle) ? fallback : text.AppRuntimeRequirementPromptTitle,
            "AppRuntimeRequirementPromptBody"      => string.IsNullOrWhiteSpace(text.AppRuntimeRequirementPromptBody) ? fallback : text.AppRuntimeRequirementPromptBody,
            "WindowTitle"                          => string.IsNullOrWhiteSpace(text.WindowTitle) ? fallback : text.WindowTitle,
            "Title"                                => string.IsNullOrWhiteSpace(text.Title) ? fallback : text.Title,
            "InitialStatus"                        => string.IsNullOrWhiteSpace(text.InitialStatus) ? fallback : text.InitialStatus,
            "LoadingConfigurationStatus"           => string.IsNullOrWhiteSpace(text.LoadingConfigurationStatus) ? fallback : text.LoadingConfigurationStatus,
            "ResolvingInstallationStatus"          => string.IsNullOrWhiteSpace(text.ResolvingInstallationStatus) ? fallback : text.ResolvingInstallationStatus,
            "CheckingForUpdatesStatus"             => string.IsNullOrWhiteSpace(text.CheckingForUpdatesStatus) ? fallback : text.CheckingForUpdatesStatus,
            "NoUpdatesLaunchingStatus"             => string.IsNullOrWhiteSpace(text.NoUpdatesLaunchingStatus) ? fallback : text.NoUpdatesLaunchingStatus,
            "UpdateAlreadyDownloadedStatus"        => string.IsNullOrWhiteSpace(text.UpdateAlreadyDownloadedStatus) ? fallback : text.UpdateAlreadyDownloadedStatus,
            "UpdatePromptBody"                     => string.IsNullOrWhiteSpace(text.UpdatePromptBody) ? fallback : text.UpdatePromptBody,
            "UpdatePromptTitle"                    => string.IsNullOrWhiteSpace(text.UpdatePromptTitle) ? fallback : text.UpdatePromptTitle,
            "LaunchingCurrentVersionStatus"        => string.IsNullOrWhiteSpace(text.LaunchingCurrentVersionStatus) ? fallback : text.LaunchingCurrentVersionStatus,
            "DownloadingVersionStatus"             => string.IsNullOrWhiteSpace(text.DownloadingVersionStatus) ? fallback : text.DownloadingVersionStatus,
            "DownloadingProgressStatus"            => string.IsNullOrWhiteSpace(text.DownloadingProgressStatus) ? fallback : text.DownloadingProgressStatus,
            "LaunchingUpdatedVersionStatus"        => string.IsNullOrWhiteSpace(text.LaunchingUpdatedVersionStatus) ? fallback : text.LaunchingUpdatedVersionStatus,
            "ErrorDialogTitle"                     => string.IsNullOrWhiteSpace(text.ErrorDialogTitle) ? fallback : text.ErrorDialogTitle,
            "DownloadUpdateButtonText"             => string.IsNullOrWhiteSpace(text.DownloadUpdateButtonText) ? fallback : text.DownloadUpdateButtonText,
            "LaunchCurrentVersionButtonText"       => string.IsNullOrWhiteSpace(text.LaunchCurrentVersionButtonText) ? fallback : text.LaunchCurrentVersionButtonText,
            "SkipVersionButtonText"                => string.IsNullOrWhiteSpace(text.SkipVersionButtonText) ? fallback : text.SkipVersionButtonText,
            "CancelButtonText"                     => string.IsNullOrWhiteSpace(text.CancelButtonText) ? fallback : text.CancelButtonText,
            "VerifyingIntegrityStatus"             => string.IsNullOrWhiteSpace(text.VerifyingIntegrityStatus) ? fallback : text.VerifyingIntegrityStatus,
            "ExtractingArchiveStatus"              => string.IsNullOrWhiteSpace(text.ExtractingArchiveStatus) ? fallback : text.ExtractingArchiveStatus,
            "CompressingFilesStatus"               => string.IsNullOrWhiteSpace(text.CompressingFilesStatus) ? fallback : text.CompressingFilesStatus,
            "PreparingFilesStatus"                 => string.IsNullOrWhiteSpace(text.PreparingFilesStatus) ? fallback : text.PreparingFilesStatus,
            "ValidatingInstallationStatus"         => string.IsNullOrWhiteSpace(text.ValidatingInstallationStatus) ? fallback : text.ValidatingInstallationStatus,
            "RunningPostInstallStatus"             => string.IsNullOrWhiteSpace(text.RunningPostInstallStatus) ? fallback : text.RunningPostInstallStatus,
            "FinalizingInstallationStatus"         => string.IsNullOrWhiteSpace(text.FinalizingInstallationStatus) ? fallback : text.FinalizingInstallationStatus,
            "CleaningUpStatus"                     => string.IsNullOrWhiteSpace(text.CleaningUpStatus) ? fallback : text.CleaningUpStatus,
            "ApplicationAlreadyRunningStatus"      => string.IsNullOrWhiteSpace(text.ApplicationAlreadyRunningStatus) ? fallback : text.ApplicationAlreadyRunningStatus,
            "ApplicationAlreadyRunningDialogTitle" => string.IsNullOrWhiteSpace(text.ApplicationAlreadyRunningDialogTitle) ? fallback : text.ApplicationAlreadyRunningDialogTitle,
            "ApplicationAlreadyRunningDialogBody"  => string.IsNullOrWhiteSpace(text.ApplicationAlreadyRunningDialogBody) ? fallback : text.ApplicationAlreadyRunningDialogBody,
            _                                      => fallback
        };

        return template
               .Replace("{ApplicationName}", _configuration.ApplicationName)
               .Replace("{Version}", version ?? string.Empty)
               .Replace("{Percent}", percent.HasValue ? percent.Value.ToString() : string.Empty)
               .Replace("{ProcessName}", processName   ?? string.Empty)
               .Replace("{RuntimeNames}", runtimeNames ?? string.Empty)
               .Replace("{RuntimeName}", runtimeName   ?? string.Empty);
    }

    private void ApplyAppearance(AppearanceConfiguration appearance)
    {
        var useDarkMode           = appearance.UseDarkMode;
        var backgroundColor       = ResolveColor(appearance.BackgroundColor, useDarkMode ? Color.FromArgb(32, 32, 32) : Color.White);
        var foregroundColor       = ResolveColor(appearance.ForegroundColor, useDarkMode ? Color.FromArgb(245, 245, 245) : Color.FromArgb(24, 24, 24));
        var secondaryTextColor    = ResolveColor(appearance.SecondaryTextColor, useDarkMode ? Color.FromArgb(210, 210, 210) : Color.FromArgb(60, 60, 60));
        var bannerBackgroundColor = ResolveColor(appearance.BannerBackgroundColor, useDarkMode ? Color.FromArgb(22, 22, 22) : Color.FromArgb(30, 49, 76));

        BackColor                = backgroundColor;
        c_ContentPanel.BackColor = backgroundColor;
        c_TitleLabel.ForeColor   = foregroundColor;
        c_StatusLabel.ForeColor  = secondaryTextColor;
        c_BannerImage.BackColor  = bannerBackgroundColor;

        if (appearance.UseDarkTitleBar || useDarkMode)
        {
            ApplyDarkTitleBar();
        }
    }

    private void ApplyDarkTitleBar()
    {
        if (!IsHandleCreated)
        {
            CreateControl();
        }

        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            var enabled = 1;
            if (DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            }
        }
        catch { /*Ignored*/ }
    }

    private static Color ResolveColor(string configuredColor, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(configuredColor);
        }
        catch
        {
            return fallback;
        }
    }

    private void TryLoadBanner(string bannerImageBase64)
    {
        if (string.IsNullOrWhiteSpace(bannerImageBase64))
        {
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(bannerImageBase64);
            using (var memoryStream = new MemoryStream(bytes))
            {
                c_BannerImage.Image = Image.FromStream(memoryStream);
            }
        }
        catch
        {
            c_BannerImage.Image = null;
        }
    }

    private void TryLoadWindowIcon(string windowIconBase64)
    {
        if (string.IsNullOrWhiteSpace(windowIconBase64))
        {
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(windowIconBase64);
            using (var memoryStream = new MemoryStream(bytes))
            {
                Icon = new Icon(memoryStream);
            }
        }
        catch
        {
            Icon = null;
        }
    }
}
