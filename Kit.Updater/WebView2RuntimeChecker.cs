using Microsoft.Win32;
using System.Net.Http;
using System.Diagnostics;

namespace Kit.Updater;

internal static class WebView2RuntimeChecker
{
    private const string RuntimeName      = "Evergreen Bootstrapper";
    private const string DownloadUrl      = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string RegistryPath     = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private const string VersionValueName = "pv";

    internal static bool IsWebView2RuntimeInstalled()
        => TryGetRuntimeVersion(RegistryHive.LocalMachine, RegistryView.Registry32)
           || TryGetRuntimeVersion(RegistryHive.CurrentUser, RegistryView.Registry32);

    internal static async Task DownloadAndInstallWebView2RuntimeAsync(IProgress<InstallationProgress> progress,
                                                                      CancellationToken               ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():B}.exe");

        try
        {
            progress.Report(new InstallationProgress(InstallationPhase.DownloadingWebView2Runtime, RuntimeName, 0, null));

            using (var response = await UpdaterHttpClient.Shared.GetAsync(
                       DownloadUrl,
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
                                InstallationPhase.DownloadingWebView2Runtime,
                                RuntimeName,
                                read,
                                size))).ConfigureAwait(false);
                }
            }

            progress.Report(new InstallationProgress(InstallationPhase.InstallingWebView2Runtime, RuntimeName, null, null));

            var startInfo = new ProcessStartInfo
            {
                FileName        = tempPath,
                Arguments       = "/silent /install",
                UseShellExecute = true,
                Verb            = "runas"
            };

            var result = await ProcessExecution.RunAsync(startInfo, ct).ConfigureAwait(false);
            if (result.ExitCode != 0 && result.ExitCode != 3010)
            {
                throw new InvalidOperationException($"Microsoft Edge WebView2 Runtime installer failed with exit code {result.ExitCode}.");
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

    private static bool TryGetRuntimeVersion(RegistryHive hive, RegistryView view)
    {
        try
        {
            using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (var runtimeKey = baseKey.OpenSubKey(RegistryPath))
            {
                var versionText = runtimeKey?.GetValue(VersionValueName) as string;
                if (string.IsNullOrWhiteSpace(versionText) || !Version.TryParse(versionText, out var parsed))
                {
                    return false;
                }

                return parsed > new Version(0, 0, 0, 0);
            }
        }
        catch
        {
            return false;
        }
    }
}
