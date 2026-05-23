using Microsoft.Win32;
using System.Diagnostics;

namespace Kit.Updater;

internal static class AppRuntimeChecker
{
    private const string DownloadUrl = "https://aka.ms/windowsappsdk/2.0/latest/windowsappruntimeinstall-x64.exe";

    internal static bool IsWindowsAppRuntimeInstalled() => CheckRegistryHive(Registry.LocalMachine)
                                                           || CheckRegistryHive(Registry.CurrentUser);

    internal static async Task DownloadAndInstallAppRuntimeAsync(IProgress<InstallationProgress> progress, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"windowsappruntimeinstall-{Guid.NewGuid():N}.exe");

        try
        {
            progress.Report(new InstallationProgress(InstallationPhase.DownloadingAppRuntime, "2.0", 0, null));

            using (var response = await UpdaterHttpClient.Shared.GetAsync(DownloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await DownloadTransfer.CopyToFileAsync(
                                              responseStream,
                                              tempPath,
                                              contentLength,
                                              ct,
                                              (totalRead, total) => progress.Report(new InstallationProgress(InstallationPhase.DownloadingAppRuntime, "2.0", totalRead, total)))
                                          .ConfigureAwait(false);
                }
            }

            progress.Report(new InstallationProgress(InstallationPhase.InstallingAppRuntime, "2.0", null, null));

            var startInfo = new ProcessStartInfo
            {
                FileName        = tempPath,
                Arguments       = "--quiet",
                UseShellExecute = true,
                Verb            = "runas"
            };

            var result = await ProcessExecution.RunAsync(startInfo, ct).ConfigureAwait(false);

            if (result.ExitCode != 0 && result.ExitCode != 3010)
            {
                throw new InvalidOperationException($"Windows App Runtime installer failed with exit code {result.ExitCode}.");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /*Ignored*/
                }
            }
        }
    }

    private static bool CheckRegistryHive(RegistryKey hive)
    {
        const string path = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\" +
                            @"CurrentVersion\AppModel\Repository\Packages";

        using (var key = hive.OpenSubKey(path, writable: false))
        {
            if (key == null)
                return false;

            if (key.GetSubKeyNames().Any(subkeyName => subkeyName.StartsWith("Microsoft.WinAppRuntime.DDLM.2.0.", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
