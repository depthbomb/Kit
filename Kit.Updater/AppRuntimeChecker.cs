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
        var tempPath     = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():B}.exe");

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
        // The semantic version (Major.Minor) is encoded in the package family name itself, as the suffix after
        // "Microsoft.WindowsAppRuntime." and before the first '_'. The field between the first and second '_' is an
        // internal build number (e.g. 8000.879.2017.0) and must NOT be used for version comparison - it is always
        // numerically much larger than any semantic version, which would cause the installed check to always pass
        // falsely.

        const string prefix = "Microsoft.WindowsAppRuntime.";

        // Strip the known prefix to get e.g. "1.8_8000.879.2017.0_x64__8wekyb3d8bbwe"
        var rest = packageName.Substring(prefix.Length);

        // The part before the first underscore is the semantic version e.g. "1.8" CBS/Main/other non-framework
        // sub-packages have non-numeric names here (e.g. "CBS.1.6") and will be correctly rejected by Version.TryParse.
        var firstUnderscore = rest.IndexOf('_');
        var versionPart     = firstUnderscore >= 0 ? rest.Substring(0, firstUnderscore) : rest;

        return Version.TryParse(versionPart, out var v) ? v : null;
    }

    private static string GetDownloadUrl(Version version)
        => $"https://aka.ms/windowsappsdk/{version.Major}.{version.Minor}/{version}/windowsappruntimeinstall-x64.exe";
}
