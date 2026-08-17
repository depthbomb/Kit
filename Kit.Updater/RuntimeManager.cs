using Shared;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kit.Updater;

internal sealed class RuntimeManager
{
    private readonly Dictionary<string, List<InstalledRuntime>> _installedRuntimesCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly UpdaterConfiguration _configuration;

    private readonly SemaphoreSlim _installedRuntimesCacheGate = new(1, 1);
    private readonly Regex _installedRuntimeLineRegex = new(@"^(?<name>[\w\.]+) (?<version>[\d\.\w\-]+) \[(?<path>.*)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        var missingRuntimes   = new List<RequiredRuntimeConfiguration>();

        foreach (var required in _configuration.RequiredRuntimes)
        {
            var architecture = RuntimeArchitectureResolver.Resolve(required.Architecture);
            var installedRuntimes = await GetInstalledRuntimesAsync(architecture, ct).ConfigureAwait(false);
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

    private async Task<List<InstalledRuntime>> GetInstalledRuntimesAsync(string architecture, CancellationToken ct)
    {
        if (_installedRuntimesCache.TryGetValue(architecture, out var cachedRuntimes))
        {
            return cachedRuntimes;
        }

        await _installedRuntimesCacheGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_installedRuntimesCache.TryGetValue(architecture, out cachedRuntimes))
            {
                return cachedRuntimes;
            }

            var runtimes = new List<InstalledRuntime>();
            var startInfo = new ProcessStartInfo
            {
                FileName               = ResolveDotNetHostPath(architecture),
                Arguments              = "--list-runtimes",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            var result = await ProcessExecution.RunAsync(startInfo, ct).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                using var reader = new StringReader(result.StandardOutput);
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var match = _installedRuntimeLineRegex.Match(line);
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

            _installedRuntimesCache[architecture] = runtimes;

            return runtimes;
        }
        catch
        {
            // If dotnet is not installed or command fails, assume none are installed.
            _installedRuntimesCache[architecture] = [];
            return _installedRuntimesCache[architecture];
        }
        finally
        {
            _installedRuntimesCacheGate.Release();
        }
    }

    public async Task DownloadAndInstallRuntimeAsync(RequiredRuntimeConfiguration    runtime,
                                                     IProgress<InstallationProgress> progress,
                                                     CancellationToken               ct)
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

            var result = await ProcessExecution.RunAsync(startInfo, ct).ConfigureAwait(false);

            // 3010 is "Restart Required", which we treat as success for now.
            if (result.ExitCode != 0 && result.ExitCode != 3010)
            {
                throw new InvalidOperationException($"Runtime installer failed with exit code {result.ExitCode}.");
            }

            _installedRuntimesCache.Remove(RuntimeArchitectureResolver.Resolve(runtime.Architecture));
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

    private async Task DownloadFileAsync(string                          url,
                                         string                          targetPath,
                                         string                          version,
                                         IProgress<InstallationProgress> progress,
                                         CancellationToken               ct)
    {
        using (var response = await UpdaterHttpClient.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                await DownloadTransfer.CopyToFileAsync(
                                          responseStream,
                                          targetPath,
                                          contentLength,
                                          ct,
                                          (totalRead, total) => progress.Report(new InstallationProgress(InstallationPhase.DownloadingRuntime, version, totalRead, total)))
                                      .ConfigureAwait(false);
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

        var architecture = RuntimeArchitectureResolver.Resolve(runtime.Architecture);
        return $"https://aka.ms/dotnet/{majorMinor}/{runtime.Type}-win-{architecture}.exe";
    }

    private static string ResolveDotNetHostPath(string architecture)
    {
        string? programFiles;
        if (string.Equals(architecture, "x86", StringComparison.OrdinalIgnoreCase))
        {
            programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        }
        else
        {
            programFiles = Environment.GetEnvironmentVariable("ProgramW6432")
                           ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        return string.IsNullOrWhiteSpace(programFiles)
            ? "dotnet"
            : Path.Combine(programFiles!, "dotnet", "dotnet.exe");
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
