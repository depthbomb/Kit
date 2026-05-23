using Shared;
using System.Net.Http;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kit.Updater;

internal sealed class RuntimeManager
{
    private readonly UpdaterConfiguration _configuration;

    public RuntimeManager(UpdaterConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<RequiredRuntimeConfiguration>> GetMissingRuntimesAsync(CancellationToken ct)
    {
        if (_configuration.RequiredRuntimes.Count == 0)
        {
            return [];
        }

        var installedRuntimes = await GetInstalledRuntimesAsync(ct).ConfigureAwait(false);
        var missingRuntimes   = new List<RequiredRuntimeConfiguration>();

        foreach (var required in _configuration.RequiredRuntimes)
        {
            if (!ApplicationVersion.TryParse(required.Version, out var requiredVersion))
            {
                continue;
            }

            var isInstalled = installedRuntimes.Any(installed =>
                string.Equals(installed.Name, required.Name, StringComparison.OrdinalIgnoreCase) &&
                installed.Version.CompareTo(requiredVersion) >= 0);
            if (!isInstalled)
            {
                missingRuntimes.Add(required);
            }
        }

        return missingRuntimes;
    }

    private async Task<List<InstalledRuntime>> GetInstalledRuntimesAsync(CancellationToken ct)
    {
        var runtimes = new List<InstalledRuntime>();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName               = "dotnet",
                Arguments              = "--list-runtimes",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return runtimes;
                }

                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

                await Task.Run(process.WaitForExit, ct).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    return runtimes;
                }

                var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"^(?<name>[\w\.]+) (?<version>[\d\.\w\-]+) \[(?<path>.*)\]$");
                    if (match.Success)
                    {
                        var name        = match.Groups["name"].Value;
                        var versionText = match.Groups["version"].Value;
                        if (ApplicationVersion.TryParse(versionText, out var version))
                        {
                            runtimes.Add(new InstalledRuntime(name, version!));
                        }
                    }
                }
            }
        }
        catch
        {
            // If dotnet is not installed or command fails, assume none are installed.
        }

        return runtimes;
    }

    public async Task DownloadAndInstallRuntimeAsync(RequiredRuntimeConfiguration runtime, IProgress<InstallationProgress> progress, CancellationToken ct)
    {
        var url      = GetDownloadUrl(runtime);
        var tempPath = Path.Combine(Path.GetTempPath(), $"dotnet-runtime-installer-{Guid.NewGuid():N}.exe");

        try
        {
            await DownloadFileAsync(url, tempPath, runtime.Version, progress, ct).ConfigureAwait(false);

            progress.Report(new InstallationProgress(InstallationPhase.InstallingRuntime, runtime.Version, null, null));

            var startInfo = new ProcessStartInfo
            {
                FileName        = tempPath,
                Arguments       = "/install /quiet /norestart",
                UseShellExecute = true,
                Verb            = "runas"
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Failed to start runtime installer.");
                await Task.Run(process.WaitForExit, ct).ConfigureAwait(false);

                // 3010 is "Restart Required", which we treat as success for now.
                if (process.ExitCode != 0 && process.ExitCode != 3010)
                {
                    throw new InvalidOperationException($"Runtime installer failed with exit code {process.ExitCode}.");
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

    private async Task DownloadFileAsync(string url, string targetPath, string version, IProgress<InstallationProgress> progress, CancellationToken ct)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Updater/1.0");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var  buffer    = new byte[81920];
                    long totalRead = 0;
                    int  bytesRead;
                    while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;
                        progress.Report(new InstallationProgress(InstallationPhase.DownloadingRuntime, version, totalRead, contentLength));
                    }
                }
            }
        }
    }

    private string GetDownloadUrl(RequiredRuntimeConfiguration runtime)
    {
        if (!ApplicationVersion.TryParse(runtime.Version, out var version))
        {
            throw new InvalidOperationException("Invalid runtime version: " + runtime.Version);
        }

        var majorMinor = version!.NumericSegments.Count >= 2
            ? $"{version.NumericSegments[0]}.{version.NumericSegments[1]}"
            : $"{version.NumericSegments[0]}.0";

        return $"https://aka.ms/dotnet/{majorMinor}/{runtime.Type}-win-x64.exe";
    }

    private sealed class InstalledRuntime
    {
        public InstalledRuntime(string name, ApplicationVersion version)
        {
            Name    = name;
            Version = version;
        }

        public string             Name    { get; }
        public ApplicationVersion Version { get; }
    }
}
