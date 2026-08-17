using Shared;

namespace Kit.Updater;

internal sealed class HeadlessUpdater
{
    public async Task<int> RunAsync(UpdaterConfiguration      configuration,
                                    UpdaterCommandLineOptions options,
                                    CancellationToken         ct)
    {
        DiagnosticLog.Initialize(configuration.ApplicationName);
        if (!await PrerequisitesAreInstalledAsync(configuration, ct).ConfigureAwait(false))
        {
            Write(options, "One or more required application runtimes are missing.");
            return UpdaterExitCode.PrerequisiteMissing;
        }

        var runtime = new UpdaterRuntime(configuration, AppDomain.CurrentDomain.BaseDirectory);
        var currentInstallation = runtime.ResolveCurrentInstallation();
        var updateResult = await runtime.CheckForUpdateAsync(currentInstallation, ct).ConfigureAwait(false);
        var plan = UpdatePolicyEvaluator.CreatePlan(configuration.UpdatePolicy, currentInstallation, updateResult);

        if (options.Mode == UpdaterCommandMode.Check)
        {
            var available = plan.Kind is UpdatePlanKind.LaunchInstalledUpdate
                or UpdatePlanKind.PromptForApplicationUpdate
                or UpdatePlanKind.InstallApplicationUpdate
                or UpdatePlanKind.InstallUpdaterUpdate;
            Write(options, available ? "Update available." : "No update available.");
            return available ? UpdaterExitCode.UpdateAvailable : UpdaterExitCode.Success;
        }

        switch (plan.Kind)
        {
            case UpdatePlanKind.LaunchCurrent:
                if (!options.NoLaunch && plan.Installation != null)
                {
                    runtime.Launch(plan.Installation);
                }

                Write(options, "No update available.");
                return UpdaterExitCode.Success;
            case UpdatePlanKind.InstallUpdaterUpdate:
                try
                {
                    await runtime.DownloadAndInstallUpdateAsync(plan.Update!, null, ct).ConfigureAwait(false);
                }
                catch (SelfUpdateRestartRequiredException)
                {
                    Write(options, "Updater refresh started.");
                    return UpdaterExitCode.SelfUpdateStarted;
                }

                break;
            case UpdatePlanKind.LaunchInstalledUpdate:
                if (!options.NoLaunch)
                {
                    await runtime.LaunchAndVerifyAsync(plan.Installation!, currentInstallation, ct).ConfigureAwait(false);
                }

                Write(options, "Previously downloaded update selected.");
                return UpdaterExitCode.UpdateInstalled;
            case UpdatePlanKind.PromptForApplicationUpdate:
            case UpdatePlanKind.InstallApplicationUpdate:
                if (runtime.IsApplicationRunning())
                {
                    Write(options, "The application is currently running.");
                    return UpdaterExitCode.ApplicationRunning;
                }

                var installed = await runtime.DownloadAndInstallUpdateAsync(plan.Update!, null, ct).ConfigureAwait(false);
                if (!options.NoLaunch)
                {
                    var healthy = await runtime.LaunchAndVerifyAsync(installed, currentInstallation, ct).ConfigureAwait(false);
                    if (!healthy && currentInstallation != null)
                    {
                        runtime.Launch(currentInstallation);
                    }
                }

                Write(options, "Update installed.");
                return UpdaterExitCode.UpdateInstalled;
        }

        return UpdaterExitCode.Failure;
    }

    private static async Task<bool> PrerequisitesAreInstalledAsync(UpdaterConfiguration configuration, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(configuration.RequiredAppRuntimeVersion)
            && (!Version.TryParse(configuration.RequiredAppRuntimeVersion, out var requiredVersion)
                || !AppRuntimeChecker.IsWindowsAppRuntimeInstalled(requiredVersion, configuration.RequiredAppRuntimeArchitecture)))
        {
            return false;
        }

        if (configuration.RequireWebView2Runtime && !WebView2RuntimeChecker.IsWebView2RuntimeInstalled())
        {
            return false;
        }

        return (await new RuntimeManager(configuration).GetMissingRuntimesAsync(ct).ConfigureAwait(false)).Count == 0;
    }

    private static void Write(UpdaterCommandLineOptions options, string message)
    {
        if (!options.Silent)
        {
            Console.WriteLine(message);
        }
    }
}
