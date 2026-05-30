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

            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            var configuration = serializer.Deserialize<UpdaterConfiguration>(configurationJson)
                                ?? throw new InvalidOperationException("The updater configuration is invalid.");

            UpdaterConfigurationValidator.Validate(configuration);
            view.ApplyConfiguration(configuration);
            view.SetStatus(UiTextKey.LoadingConfigurationStatus, "Loading updater configuration...", true);

            var progress = new Progress<InstallationProgress>(view.ReportProgress);

            if (!string.IsNullOrWhiteSpace(configuration.RequiredAppRuntimeVersion))
            {
                var requiredVersion = Version.Parse(configuration.RequiredAppRuntimeVersion);

                view.SetStatus(UiTextKey.CheckingAppRuntimesStatus, "Checking Windows App Runtime...", true);

                if (!AppRuntimeChecker.IsWindowsAppRuntimeInstalled(requiredVersion))
                {
                    if (!view.ConfirmAppRuntimeInstallation())
                    {
                        view.CloseWindow();
                        return;
                    }

                    view.SetStatus(UiTextKey.InstallingAppRuntimeStatus, "Installing Windows App Runtime...", false);
                    await AppRuntimeChecker.DownloadAndInstallAppRuntimeAsync(requiredVersion, progress, ct);
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

            EnsureUpdatePolicySatisfied(configuration.UpdatePolicy, currentInstallation, updateResult.AvailableUpdate);

            if (!updateResult.IsUpdateAvailable || updateResult.AvailableUpdate == null)
            {
                if (currentInstallation == null)
                {
                    throw new InvalidOperationException("No local installation was found and no update is available to perform a fresh install.");
                }

                if (updateResult.WasSkipped)
                {
                    view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "Launching the current version...", true);
                }
                else
                {
                    view.SetStatus(UiTextKey.NoUpdatesLaunchingStatus, "No updates found. Launching {ApplicationName}...", true);
                }

                await LaunchAsync(view, runtime, currentInstallation, ct);
                return;
            }

            if (updateResult.AlreadyInstalled)
            {
                view.SetStatus(UiTextKey.UpdateAlreadyDownloadedStatus, "Version {Version} is already downloaded. Launching it now...", true, updateResult.AvailableUpdate.DisplayVersion);
                await LaunchAsync(view, runtime, updateResult.LaunchInstallation, ct);
                return;
            }

            if (currentInstallation != null)
            {
                // Updater updates are always treated as required, skipping them (for the session or permanently) would
                // bypass a mandatory installer update, so only Install/Cancel is offered.
                var requireImmediateInstall = updateResult.AvailableUpdate.IsUpdaterUpdate || IsUpdateRequired(configuration.UpdatePolicy, currentInstallation);
                switch (view.PromptForUpdate(updateResult.AvailableUpdate, !requireImmediateInstall, !requireImmediateInstall))
                {
                    case UpdatePromptChoice.Cancel:
                        view.CloseWindow();
                        return;
                    case UpdatePromptChoice.SkipForSession:
                        view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "Launching current version...", true);
                        await LaunchAsync(view, runtime, currentInstallation, ct);
                        return;
                    case UpdatePromptChoice.SkipVersion:
                        runtime.SkipVersion(updateResult.AvailableUpdate.Version.NormalizedValue);
                        view.SetStatus(UiTextKey.LaunchingCurrentVersionStatus, "Launching current version...", true);
                        await LaunchAsync(view, runtime, currentInstallation, ct);
                        return;
                }
            }

            if (!await EnsureApplicationNotRunningForInstallAsync(view, runtime, updateResult.AvailableUpdate.DisplayVersion, ct))
            {
                view.CloseWindow();
                return;
            }

            view.SetStatus(UiTextKey.DownloadingVersionStatus, "Downloading version {Version}...", false, updateResult.AvailableUpdate.DisplayVersion);
            var installedUpdate = await runtime.DownloadAndInstallUpdateAsync(updateResult.AvailableUpdate, progress, ct);

            view.SetStatus(UiTextKey.LaunchingUpdatedVersionStatus, "Launching version {Version}...", true, installedUpdate.Version.NormalizedValue);
            await LaunchAsync(view, runtime, installedUpdate, ct);
        }
        catch (OperationCanceledException)
        {
            view.CloseWindow();
        }
        catch (SelfUpdateRestartRequiredException)
        {
            view.CloseWindow();
        }
        catch (Exception exception)
        {
            view.ShowError(exception.Message, "Updater Error");
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

    private static bool IsUpdateRequired(UpdatePolicyConfiguration policy, LocalApplicationInstallation? currentInstallation)
    {
        var mode = policy.Mode.Trim();
        if (mode.Length == 0 || string.Equals(mode, "optional", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(mode, "required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(mode, "minimum-version-required", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (currentInstallation == null || !ApplicationVersion.TryParse(policy.MinimumVersion, out var minimumVersion))
        {
            return true;
        }

        return currentInstallation.Version.CompareTo(minimumVersion) < 0;
    }

    private static void EnsureUpdatePolicySatisfied(UpdatePolicyConfiguration     policy,
                                                    LocalApplicationInstallation? currentInstallation,
                                                    AvailableUpdate?              availableUpdate)
    {
        var mode = policy.Mode.Trim();
        if (!string.Equals(mode, "minimum-version-required", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (currentInstallation == null)
        {
            return;
        }

        if (!ApplicationVersion.TryParse(policy.MinimumVersion, out var minimumVersion))
        {
            throw new InvalidOperationException("updatePolicy.minimumVersion must be a valid version when updatePolicy.mode is minimum-version-required.");
        }

        if (currentInstallation.Version.CompareTo(minimumVersion) >= 0)
        {
            return;
        }

        if (availableUpdate == null)
        {
            throw new InvalidOperationException("The installed application version is below the required minimum version and no update is available.");
        }

        if (availableUpdate.Version.CompareTo(minimumVersion) < 0)
        {
            throw new InvalidOperationException("The available update does not satisfy the configured minimum required version.");
        }
    }
}
