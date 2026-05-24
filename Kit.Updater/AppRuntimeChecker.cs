using Microsoft.Win32;
using System.Net.Http;
using System.Diagnostics;

namespace Kit.Updater;

internal static class AppRuntimeChecker
{
    private const string PackagesPath = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\" +
                                        @"CurrentVersion\AppModel\Repository\Packages";

    internal static bool IsWindowsAppRuntimeInstalled(Version minVersion)
    {
        var installed = GetHighestInstalledRuntimeVersion();

        return installed != null && installed >= minVersion;
    }

    internal static async Task DownloadAndInstallAppRuntimeAsync(Version                         requiredVersion,
                                                                 IProgress<InstallationProgress> progress,
                                                                 CancellationToken               ct)
    {
        var url          = GetDownloadUrl(requiredVersion);
        var versionLabel = $"{requiredVersion.Major}.{requiredVersion.Minor}";
        var tempPath     = Path.Combine(Path.GetTempPath(), $"windowsappruntimeinstall-{Guid.NewGuid():N}.exe");

        try
        {
            progress.Report(new InstallationProgress(
                InstallationPhase.DownloadingAppRuntime,
                versionLabel,
                0,
                null));

            using (var response = await UpdaterHttpClient.Shared.GetAsync(
                       url,
                       HttpCompletionOption.ResponseHeadersRead,
                       ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength;

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await DownloadTransfer.CopyToFileAsync(
                                              stream,
                                              tempPath,
                                              total,
                                              ct,
                                              (read, size) =>
                                                  progress.Report(new InstallationProgress(
                                                      InstallationPhase.DownloadingAppRuntime,
                                                      versionLabel,
                                                      read,
                                                      size)))
                                          .ConfigureAwait(false);
                }
            }

            progress.Report(new InstallationProgress(
                InstallationPhase.InstallingAppRuntime,
                versionLabel,
                null,
                null));

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
                throw new InvalidOperationException(
                    $"Windows App Runtime installer failed with exit code {result.ExitCode}.");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                /* Ignored */
            }
        }
    }

    private static Version? GetHighestInstalledRuntimeVersion()
    {
        Version? best = null;

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using (var root = hive.OpenSubKey(PackagesPath))
            {
                if (root == null)
                    continue;

                foreach (var name in root.GetSubKeyNames())
                {
                    // Microsoft.WindowsAppRuntime.1_...
                    // Microsoft.WindowsAppRuntime.2_...
                    // Microsoft.WindowsAppRuntime.3_...

                    if (!name.StartsWith("Microsoft.WindowsAppRuntime.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var version = TryExtractVersion(name);
                    if (version == null)
                        continue;

                    if (best == null || version > best)
                        best = version;
                }
            }
        }

        return best;
    }

    private static Version? TryExtractVersion(string packageName)
    {
        // Format:
        // Microsoft.WindowsAppRuntime.X_2.1.3.0_x64__...

        var firstUnderscore = packageName.IndexOf('_');
        if (firstUnderscore < 0 || firstUnderscore == packageName.Length - 1)
            return null;

        var remainder = packageName.Substring(firstUnderscore + 1);

        var secondUnderscore = remainder.IndexOf('_');
        if (secondUnderscore < 0)
            return null;

        var versionString = remainder.Substring(0, secondUnderscore);

        return Version.TryParse(versionString, out var v) ? v : null;
    }

    private static string GetDownloadUrl(Version version)
        => $"https://aka.ms/windowsappsdk/{version.Major}.{version.Minor}/latest/windowsappruntimeinstall-x64.exe";
}
