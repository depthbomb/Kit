using Shared;
using System.Runtime.InteropServices;

namespace Kit.Updater.Forms;

internal sealed partial class MainForm : Form, IUpdaterView
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode           = 20;

    private UpdaterConfiguration _configuration;

    private readonly CancellationTokenSource _cts;
    private readonly UiTextResolver          _textResolver;

    public MainForm()
    {
        InitializeComponent();

        _cts           = new CancellationTokenSource();
        _configuration = new UpdaterConfiguration();
        _textResolver  = new UiTextResolver();
    }

    public void ApplyConfiguration(UpdaterConfiguration configuration)
    {
        _configuration = configuration;

        Text               = GetText(UiTextKey.WindowTitle, "{ApplicationName} Bootstrapper");
        c_TitleLabel.Text  = GetText(UiTextKey.Title, "{ApplicationName}");
        c_StatusLabel.Text = GetText(UiTextKey.InitialStatus, "Starting...");

        ApplyAppearance(configuration.Appearance);
        TryLoadWindowIcon(configuration.WindowIconBase64);
        TryLoadBanner(configuration.BannerImageBase64);
    }

    public void SetStatus(UiTextKey key,
                          string    fallback,
                          bool      indeterminate,
                          string?   version      = null,
                          int?      percent      = null,
                          string?   processName  = null,
                          string?   runtimeNames = null,
                          string?   runtimeName  = null)
    {
        SetStatusMessage(GetText(key, fallback, version, percent, processName, runtimeNames, runtimeName), indeterminate);
    }

    public bool ConfirmRuntimeInstallation(IReadOnlyList<RequiredRuntimeConfiguration> missingRuntimes)
    {
        var runtimeNames = string.Join(", ", missingRuntimes.Select(r => $"{r.Name} {r.Version}"));
        var result = MessageBox.Show(
            this,
            GetText(UiTextKey.RuntimeRequirementPromptBody, "This application requires the following .NET runtimes:\r\n\r\n{RuntimeNames}\r\n\r\nWould you like to download and install them now?", runtimeNames: runtimeNames),
            GetText(UiTextKey.RuntimeRequirementPromptTitle, ".NET Runtime Required"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }

    public bool ConfirmAppRuntimeInstallation()
    {
        var result = MessageBox.Show(
            this,
            GetText(UiTextKey.AppRuntimeRequirementPromptBody, "This application requires the Windows App Runtime to be installed.\r\n\r\nWould you like to download and install it now?"),
            GetText(UiTextKey.AppRuntimeRequirementPromptTitle, "Windows App Runtime Required"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }

    public bool ConfirmWebView2RuntimeInstallation()
    {
        var result = MessageBox.Show(
            this,
            GetText(UiTextKey.WebView2RuntimeRequirementPromptBody, "This application requires the Microsoft Edge WebView2 Runtime to be installed.\r\n\r\nWould you like to download and install it now?"),
            GetText(UiTextKey.WebView2RuntimeRequirementPromptTitle, "Microsoft Edge WebView2 Runtime Required"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }

    public UpdatePromptChoice PromptForUpdate(AvailableUpdate update, bool allowSkipForSession, bool allowSkipVersion)
    {
        using (var prompt = new UpdatePromptForm(new UpdatePromptRequest
               {
                   WindowTitle              = GetText(UiTextKey.UpdatePromptTitle, "{ApplicationName} Update Available", update.DisplayVersion),
                   Message                  = GetText(UiTextKey.UpdatePromptBody, "A newer version of {ApplicationName} is available ({Version}).", update.DisplayVersion),
                   DownloadButtonText       = GetText(UiTextKey.DownloadUpdateButtonText, "Download update"),
                   SkipForSessionButtonText = GetText(UiTextKey.SkipForSessionButtonText, "Skip for now"),
                   SkipVersionButtonText    = GetText(UiTextKey.SkipVersionButtonText, "Skip this version"),
                   CancelButtonText         = GetText(UiTextKey.CancelButtonText, "Cancel"),
                   AllowSkipForSession      = allowSkipForSession,
                   AllowSkipVersion         = allowSkipVersion
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
            GetText(UiTextKey.ApplicationAlreadyRunningDialogBody, "{ApplicationName} is currently running. Close it before installing version {Version}, then choose Retry. Choose Cancel to stop the update.", version, processName: processName),
            GetText(UiTextKey.ApplicationAlreadyRunningDialogTitle, "{ApplicationName} is running"),
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
        MessageBox.Show(this, message, GetText(UiTextKey.ErrorDialogTitle, titleFallback), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    #region Overrides
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
    #endregion

    private Task RunWorkflowAsync() => new UpdaterWorkflow().RunAsync(this, _cts.Token);

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
                    c_StatusLabel.Text  = GetText(UiTextKey.DownloadingProgressStatus, "Downloading update... {Percent}%", progress.Version, percentage);
                }
                else
                {
                    SetStatusMessage(GetText(UiTextKey.DownloadingVersionStatus, "Downloading version {Version}...", progress.Version), true);
                }

                break;
            case InstallationPhase.VerifyingIntegrity:
                SetStatusMessage(GetText(UiTextKey.VerifyingIntegrityStatus, "Verifying download..."), true);
                break;
            case InstallationPhase.ExtractingArchive:
                SetStatusMessage(GetText(UiTextKey.ExtractingArchiveStatus, "Extracting archive..."), true);
                break;
            case InstallationPhase.CompressingFiles:
                SetStatusMessage(GetText(UiTextKey.CompressingFilesStatus, "Compressing files..."), true);
                break;
            case InstallationPhase.PreparingFiles:
                SetStatusMessage(GetText(UiTextKey.PreparingFilesStatus, "Preparing files..."), true);
                break;
            case InstallationPhase.ValidatingInstallation:
                SetStatusMessage(GetText(UiTextKey.ValidatingInstallationStatus, "Validating installation..."), true);
                break;
            case InstallationPhase.RunningPostInstall:
                SetStatusMessage(GetText(UiTextKey.RunningPostInstallStatus, "Running post-install steps..."), true);
                break;
            case InstallationPhase.FinalizingInstallation:
                SetStatusMessage(GetText(UiTextKey.FinalizingInstallationStatus, "Finalizing installation..."), true);
                break;
            case InstallationPhase.CleaningUp:
                SetStatusMessage(GetText(UiTextKey.CleaningUpStatus, "Cleaning up temporary files..."), true);
                break;
            case InstallationPhase.CheckingRuntimes:
                SetStatusMessage(GetText(UiTextKey.CheckingRuntimesStatus, "Checking for required .NET runtimes..."), true);
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
                    c_StatusLabel.Text  = GetText(UiTextKey.DownloadingRuntimeProgressStatus, "Downloading .NET runtime... {Percent}%", progress.Version, percentage);
                }
                else
                {
                    SetStatusMessage(GetText(UiTextKey.InstallingRuntimeStatus, "Installing runtime version {Version}...", progress.Version), true);
                }

                break;
            case InstallationPhase.InstallingRuntime:
                SetStatusMessage(GetText(UiTextKey.InstallingRuntimeStatus, "Installing runtime version {Version}...", progress.Version), true);
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
                    c_StatusLabel.Text  = GetText(UiTextKey.DownloadingAppRuntimeProgressStatus, "Downloading Windows App Runtime... {Percent}%", percent: percentage);
                }
                else
                {
                    SetStatusMessage(GetText(UiTextKey.InstallingAppRuntimeStatus, "Installing Windows App Runtime..."), true);
                }

                break;
            case InstallationPhase.InstallingAppRuntime:
                SetStatusMessage(GetText(UiTextKey.InstallingAppRuntimeStatus, "Installing Windows App Runtime..."), true);
                break;
            case InstallationPhase.DownloadingWebView2Runtime:
                if (progress.TotalBytes is > 0)
                {
                    var percentage = (int)Math.Max(0, Math.Min(100, progress.BytesReceived.GetValueOrDefault() * 100 / progress.TotalBytes.Value));
                    if (c_ProgressBar.Style != ProgressBarStyle.Continuous)
                    {
                        c_ProgressBar.Style = ProgressBarStyle.Continuous;
                    }

                    c_ProgressBar.Value = percentage;
                    c_StatusLabel.Text  = GetText(UiTextKey.DownloadingWebView2RuntimeProgressStatus, "Downloading Microsoft Edge WebView2 Runtime... {Percent}%", percent: percentage);
                }
                else
                {
                    SetStatusMessage(GetText(UiTextKey.InstallingWebView2RuntimeStatus, "Installing Microsoft Edge WebView2 Runtime..."), true);
                }

                break;
            case InstallationPhase.InstallingWebView2Runtime:
                SetStatusMessage(GetText(UiTextKey.InstallingWebView2RuntimeStatus, "Installing Microsoft Edge WebView2 Runtime..."), true);
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

    private string GetText(UiTextKey key,
                           string    fallback,
                           string?   version      = null,
                           int?      percent      = null,
                           string?   processName  = null,
                           string?   runtimeNames = null,
                           string?   runtimeName  = null)
        => _textResolver.Resolve(_configuration, key, fallback, version, percent, processName, runtimeNames, runtimeName);

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
        catch
        {
            /*Ignored*/
        }
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
