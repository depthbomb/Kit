using Microsoft.Win32;
using System.Net.Http;
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

            using (var response = await UpdaterHttpClient.Shared.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var  buffer    = new byte[81920];
                    long totalRead = 0;
                    int  bytesRead;
                    while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;
                        progress.Report(new InstallationProgress(InstallationPhase.DownloadingAppRuntime, "2.0", totalRead, contentLength));
                    }
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

            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Failed to start Windows App Runtime installer.");

                await Task.Run(process.WaitForExit, ct).ConfigureAwait(false);

                if (process.ExitCode != 0 && process.ExitCode != 3010)
                {
                    throw new InvalidOperationException($"Windows App Runtime installer failed with exit code {process.ExitCode}.");
                }
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

            if (key.GetSubKeyNames().Any(subkeyName => subkeyName.StartsWith("Microsoft.WinAppRuntime.DDLM.2.0.", StringComparison.OrdinalIgnoreCase))
               )
            {
                return true;
            }
        }

        return false;
    }
}
