using Shared;
using Kit.Updater.Forms;
using System.Reflection;

namespace Kit.Updater;

internal interface IUpdaterView
{
    void ApplyConfiguration(UpdaterConfiguration configuration);

    void SetStatus(UiTextKey key,
                   string    fallback,
                   bool      indeterminate,
                   string?   version      = null,
                   int?      percent      = null,
                   string?   processName  = null,
                   string?   runtimeNames = null,
                   string?   runtimeName  = null);

    bool ConfirmRuntimeInstallation(IReadOnlyList<RequiredRuntimeConfiguration> missingRuntimes);

    bool ConfirmAppRuntimeInstallation();

    bool ConfirmWebView2RuntimeInstallation();

    UpdatePromptChoice PromptForUpdate(AvailableUpdate update, bool allowLaunchCurrent, bool allowSkipVersion);

    bool ConfirmApplicationClosedForInstall(string version, string processName);

    void ReportProgress(InstallationProgress progress);

    void ShowError(string message, string titleFallback);

    void CloseWindow();
}

internal sealed class UpdaterWorkflow
{
    public async Task RunAsync(IUpdaterView view, CancellationToken ct)
    {
        try
        {
            view.SetStatus(UiTextKey.LoadingConfigurationStatus, "Loading updater configuration...", true);

            var executablePath    = Assembly.GetExecutingAssembly().Location;
            var configurationJson = StampPayload.ReadConfigurationJson(executablePath);

            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var configuration = serializer.Deserialize<UpdaterConfiguration>(configurationJson)
                                ?? throw new InvalidOperationException("The updater configuration is invalid.");

            UpdaterConfigurationValidator.Validate(configuration);
            DiagnosticLog.Initialize(configuration.ApplicationName);
            DiagnosticLog.Info("configuration.loaded",
                new KeyValuePair<string, string?>("application", configuration.ApplicationName),
                new KeyValuePair<string, string?>("initialVersion", configuration.InitialVersion));
            view.ApplyConfiguration(configuration);
            view.SetStatus(UiTextKey.LoadingConfigurationStatus, "Loading updater configuration...", true);

            var progress = new Progress<InstallationProgress>(view.ReportProgress);

            if (!string.IsNullOrWhiteSpace(configuration.RequiredAppRuntimeVersion))
            {
                var requiredVersion = Version.Parse(configuration.RequiredAppRuntimeVersion);

                view.SetStatus(UiTextKey.CheckingAppRuntimesStatus, "Checking Windows App Runtime...", true);

                if (!AppRuntimeChecker.IsWindowsAppRuntimeInstalled(requiredVersion, configuration.RequiredAppRuntimeArchitecture))
                {
                    DiagnosticLog.Info("prerequisite.windows_app_runtime_missing",
                        new KeyValuePair<string, string?>("requiredVersion", requiredVersion.ToString()));
                    if (!view.ConfirmAppRuntimeInstallation())
                    {
                        view.CloseWindow();
                        return;
                    }

                    view.SetStatus(UiTextKey.InstallingAppRuntimeStatus, "Installing Windows App Runtime...", false);
                    await AppRuntimeChecker.DownloadAndInstallAppRuntimeAsync(requiredVersion, progress, ct, configuration.RequiredAppRuntimeArchitecture);
                }
            }

            if (configuration.RequireWebView2Runtime)
            {
                view.SetStatus(UiTextKey.CheckingWebView2RuntimeStatus, "Checking Microsoft Edge WebView2 Runtime...", true);

                if (!WebView2RuntimeChecker.IsWebView2RuntimeInstalled())
                {
                    DiagnosticLog.Info("prerequisite.webview2_missing");
                    if (!view.ConfirmWebView2RuntimeInstallation())
                    {
                        view.CloseWindow();
                        return;
                    }

                    view.SetStatus(UiTextKey.InstallingWebView2RuntimeStatus, "Installing Microsoft Edge WebView2 Runtime...", false);
                    await WebView2RuntimeChecker.DownloadAndInstallWebView2RuntimeAsync(progress, ct);
                }
            }

            var runtimeManager = new RuntimeManager(configuration);

            view.SetStatus(UiTextKey.CheckingRuntimesStatus, "Checking for required .NET runtimes...", true);

            var missingRuntimes = await runtimeManager.GetMissingRuntimesAsync(ct);
            if (missingRuntimes.Count > 0)
            {
                if (!view.ConfirmRuntimeInstallation(missingRuntimes))
                {
                    view.CloseWindow();
                    return;
                }

                foreach (var missing in missingRuntimes)
                {
                    view.SetStatus(UiTextKey.InstallingRuntimeStatus, "Installing {RuntimeName} {Version}...", false, missing.Version, runtimeName: missing.Name);
                    await runtimeManager.DownloadAndInstallRuntimeAsync(missing, progress, ct);
                }
            }

            var runtime = new UpdaterRuntime(configuration, AppDomain.CurrentDomain.BaseDirectory);

            view.SetStatus(UiTextKey.ResolvingInstallationStatus, "Resolving installed application version...", true);
            var currentInstallation = runtime.ResolveCurrentInstallation();

            view.SetStatus(UiTextKey.CheckingForUpdatesStatus, "Checking for updates...", true);
            var updateResult = await runtime.CheckForUpdateAsync(currentInstallation, ct);
            DiagnosticLog.Info("update.checked",
                new KeyValuePair<string, string?>("currentVersion", currentInstallation?.Version.NormalizedValue),
                new KeyValuePair<string, string?>("availableVersion", updateResult.AvailableUpdate?.Version.NormalizedValue),
                new KeyValuePair<string, string?>("isAvailable", updateResult.IsUpdateAvailable.ToString()));

            var plan = UpdatePolicyEvaluator.CreatePlan(configuration.UpdatePolicy, currentInstallation, updateResult);
            DiagnosticLog.Info("update.plan_created",
                new KeyValuePair<string, string?>("kind", plan.Kind.ToString()));

            switch (plan.Kind)
            {
                case UpdatePlanKind.LaunchCurrent:
                    view.SetStatus(
                        plan.WasSkipped ? UiTextKey.LaunchingCurrentVersionStatus : UiTextKey.NoUpdatesLaunchingStatus,
                        plan.WasSkipped ? "Launching the current version..." : "No updates found. Launching {ApplicationName}...",
                        true);
                    await LaunchAsync(view, runtime, plan.Installation, ct);
                    return;
                case UpdatePlanKind.LaunchInstalledUpdate:
                    view.SetStatus(UiTextKey.UpdateAlreadyDownloadedStatus, "Version {Version} is already downloaded. Launching it now...", true, plan.Update!.DisplayVersion);
                    await LaunchUpdatedAsync(view, runtime, plan.Installation!, currentInstallation, ct);
                    return;
                case UpdatePlanKind.InstallUpdaterUpdate:
                    view.SetStatus(UiTextKey.DownloadingVersionStatus, "Downloading updater update {Version}...", false, plan.Update!.DisplayVersion);
                    await runtime.DownloadAndInstallUpdateAsync(plan.Update, progress, ct);
                    return;
                case UpdatePlanKind.PromptForApplicationUpdate:
                    switch (view.PromptForUpdate(plan.Update!, true, true))
                    {
                        case UpdatePromptChoice.Cancel:
                            view.CloseWindow();
                            return;
                        case UpdatePromptChoice.SkipForSession:
                            view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "Launching current version...", true);
                            await LaunchAsync(view, runtime, plan.Installation, ct);
                            return;
                        case UpdatePromptChoice.SkipVersion:
                            runtime.SkipVersion(plan.Update!.Version.NormalizedValue);
                            view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "Launching current version...", true);
                            await LaunchAsync(view, runtime, plan.Installation, ct);
                            return;
                    }

                    break;
                case UpdatePlanKind.InstallApplicationUpdate:
                    break;
                default:
                    throw new InvalidOperationException("Unsupported update plan: " + plan.Kind);
            }

            var plannedUpdate = plan.Update ?? throw new InvalidOperationException("The update plan does not contain an update.");
            if (!await EnsureApplicationNotRunningForInstallAsync(view, runtime, plannedUpdate.DisplayVersion, ct))
            {
                view.CloseWindow();
                return;
            }

            view.SetStatus(UiTextKey.DownloadingVersionStatus, "Downloading version {Version}...", false, plannedUpdate.DisplayVersion);
            var installedUpdate = await runtime.DownloadAndInstallUpdateAsync(plannedUpdate, progress, ct);
            DiagnosticLog.Info("update.installed",
                new KeyValuePair<string, string?>("version", installedUpdate.Version.NormalizedValue));

            view.SetStatus(UiTextKey.LaunchingUpdatedVersionStatus, "Launching version {Version}...", true, installedUpdate.Version.NormalizedValue);
            await LaunchUpdatedAsync(view, runtime, installedUpdate, currentInstallation, ct);
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Info("updater.cancelled");
            view.CloseWindow();
        }
        catch (SelfUpdateRestartRequiredException)
        {
            DiagnosticLog.Info("self_update.started");
            view.CloseWindow();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("updater.failed", exception);
            view.ShowError(exception.Message + Environment.NewLine + Environment.NewLine + "Diagnostic log: " + DiagnosticLog.FilePath, "Updater Error");
            view.CloseWindow();
        }
    }

    private static async Task<bool> EnsureApplicationNotRunningForInstallAsync(IUpdaterView      view,
                                                                               UpdaterRuntime    runtime,
                                                                               string            version,
                                                                               CancellationToken ct)
    {
        while (runtime.IsApplicationRunning())
        {
            if (!view.ConfirmApplicationClosedForInstall(version, runtime.GetApplicationProcessName()))
            {
                return false;
            }

            await Task.Delay(500, ct);
        }

        return true;
    }

    private static async Task LaunchAsync(IUpdaterView                  view,
                                          UpdaterRuntime                runtime,
                                          LocalApplicationInstallation? installation,
                                          CancellationToken             ct)
    {
        if (installation == null)
        {
            return;
        }

        if (runtime.IsApplicationRunning())
        {
            view.SetStatus(UiTextKey.ApplicationAlreadyRunningStatus, "{ApplicationName} is already running.", true, installation.Version.NormalizedValue, processName: runtime.GetApplicationProcessName());
            await Task.Delay(500, ct);
            view.CloseWindow();
            return;
        }

        await Task.Delay(500, ct);
        runtime.Launch(installation);
        view.CloseWindow();
    }

    private static async Task LaunchUpdatedAsync(IUpdaterView                  view,
                                                 UpdaterRuntime                runtime,
                                                 LocalApplicationInstallation installation,
                                                 LocalApplicationInstallation? previousInstallation,
                                                 CancellationToken             ct)
    {
        if (await runtime.LaunchAndVerifyAsync(installation, previousInstallation, ct))
        {
            view.CloseWindow();
            return;
        }

        if (previousInstallation == null)
        {
            throw new InvalidOperationException("The newly installed application exited during its launch health check and no previous version is available.");
        }

        view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "The updated application failed its launch health check. Restoring the previous version...", true);
        runtime.Launch(previousInstallation);
        view.CloseWindow();
    }

}
