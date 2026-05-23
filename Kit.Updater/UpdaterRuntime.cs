using Shared;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Kit.Updater;

internal sealed class LocalApplicationInstallation
{
    public LocalApplicationInstallation(ApplicationVersion version, string directoryPath, string executablePath)
    {
        Version        = version;
        DirectoryPath  = directoryPath;
        ExecutablePath = executablePath;
    }

    public ApplicationVersion Version { get; private set; }

    public string DirectoryPath { get; private set; }

    public string ExecutablePath { get; private set; }
}

internal sealed class AvailableUpdate
{
    public AvailableUpdate(ApplicationVersion version,
                           string             downloadUrl,
                           string             displayVersion,
                           string             sha256,
                           bool               isUpdaterUpdate               = false,
                           string?            applicationPackageDownloadUrl = null,
                           string?            applicationPackageSha256      = null)
    {
        Version                       = version;
        DownloadUrl                   = downloadUrl;
        DisplayVersion                = displayVersion;
        Sha256                        = sha256;
        IsUpdaterUpdate               = isUpdaterUpdate;
        ApplicationPackageDownloadUrl = applicationPackageDownloadUrl ?? string.Empty;
        ApplicationPackageSha256      = applicationPackageSha256      ?? string.Empty;
    }

    public ApplicationVersion Version { get; private set; }

    public string DownloadUrl { get; private set; }

    public string DisplayVersion { get; private set; }

    public string Sha256 { get; private set; }

    public bool IsUpdaterUpdate { get; private set; }

    /// <summary>The URL to download the app package ZIP after an updater-installer update has already been applied.</summary>
    public string ApplicationPackageDownloadUrl { get; private set; }

    /// <summary>The SHA-256 checksum of the app package ZIP.</summary>
    public string ApplicationPackageSha256 { get; private set; }
}

internal sealed class UpdateCheckResult
{
    public UpdateCheckResult(bool                          isUpdateAvailable,
                             LocalApplicationInstallation? launchInstallation,
                             AvailableUpdate?              availableUpdate,
                             bool                          alreadyInstalled = false,
                             bool                          wasSkipped       = false)
    {
        IsUpdateAvailable  = isUpdateAvailable;
        LaunchInstallation = launchInstallation;
        AvailableUpdate    = availableUpdate;
        AlreadyInstalled   = alreadyInstalled;
        WasSkipped         = wasSkipped;
    }

    public bool IsUpdateAvailable { get; private set; }

    public LocalApplicationInstallation? LaunchInstallation { get; private set; }

    public AvailableUpdate? AvailableUpdate { get; private set; }

    public bool AlreadyInstalled { get; private set; }

    public bool WasSkipped { get; private set; }
}

internal enum InstallationPhase
{
    Downloading,
    VerifyingIntegrity,
    ExtractingArchive,
    CompressingFiles,
    PreparingFiles,
    ValidatingInstallation,
    RunningPostInstall,
    FinalizingInstallation,
    CleaningUp,
    CheckingRuntimes,
    DownloadingRuntime,
    InstallingRuntime,
    DownloadingAppRuntime,
    InstallingAppRuntime
}

internal sealed class InstallationProgress
{
    public InstallationProgress(InstallationPhase phase, string version, long? bytesReceived, long? totalBytes)
    {
        Phase         = phase;
        Version       = version;
        BytesReceived = bytesReceived;
        TotalBytes    = totalBytes;
    }

    public InstallationPhase Phase { get; private set; }

    public string Version { get; private set; }

    public long? BytesReceived { get; private set; }

    public long? TotalBytes { get; private set; }
}

internal sealed class SelfUpdateRestartRequiredException : Exception;

internal sealed class UpdaterRuntime
{
    private const string CurrentVersionFileName        = ".kit-current-version";
    private const string LastDownloadedVersionFileName = ".kit-last-downloaded-version";
    private const string SkippedVersionFileName        = ".kit-skipped-version";

    private readonly UpdaterConfiguration _configuration;
    private readonly IUpdateSource        _updateSource;
    private readonly string               _baseDirectory;
    private readonly string               _currentVersionFilePath;
    private readonly string               _lastDownloadedVersionFilePath;
    private readonly string               _skippedVersionFilePath;

    public UpdaterRuntime(UpdaterConfiguration configuration, string baseDirectory)
    {
        _configuration                 = configuration;
        _updateSource                  = UpdateSourceFactory.Create(configuration);
        _baseDirectory                 = baseDirectory;
        _currentVersionFilePath        = Path.Combine(_baseDirectory, CurrentVersionFileName);
        _lastDownloadedVersionFilePath = Path.Combine(_baseDirectory, LastDownloadedVersionFileName);
        _skippedVersionFilePath        = Path.Combine(_baseDirectory, SkippedVersionFileName);
    }

    public LocalApplicationInstallation? ResolveCurrentInstallation()
    {
        var installations = GetInstalledVersions();
        if (installations.Count == 0)
        {
            if (_configuration.Installation.AllowFreshInstall)
            {
                return null;
            }

            throw new InvalidOperationException("No local app-VERSION folders were found next to the updater.");
        }

        var preferredVersion = ReadVersionHint(_currentVersionFilePath)
                               ?? ReadVersionHint(_lastDownloadedVersionFilePath)
                               ?? ReadConfiguredInitialVersion();

        if (preferredVersion != null)
        {
            var preferredInstallation = installations.FirstOrDefault(installation => installation.Version.CompareTo(preferredVersion) == 0);
            if (preferredInstallation != null)
            {
                return preferredInstallation;
            }
        }

        return installations.OrderByDescending(installation => installation.Version).First();
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(LocalApplicationInstallation? currentInstallation, CancellationToken cancellationToken)
    {
        var availableUpdate = await _updateSource.GetAvailableUpdateAsync(cancellationToken).ConfigureAwait(false);
        if (availableUpdate == null)
        {
            return new UpdateCheckResult(false, currentInstallation, null);
        }

        // If this is an updater-installer update, check whether this bootstrapper was already stamped for
        // (and therefore installed by) this exact release version. If so, the installer has already run, and we should
        // download the app package instead of looping back to the installer. This check is stateless - the answer is
        // embedded in the binary itself.
        if (availableUpdate.IsUpdaterUpdate                                                 &&
            ApplicationVersion.TryParse(_configuration.UpdaterVersion, out var selfVersion) &&
            selfVersion                                    != null                          &&
            selfVersion.CompareTo(availableUpdate.Version) >= 0)
        {
            if (!string.IsNullOrWhiteSpace(availableUpdate.ApplicationPackageDownloadUrl))
            {
                // Switch to the app package - the installer step is already done.
                availableUpdate = new AvailableUpdate(
                    availableUpdate.Version,
                    availableUpdate.ApplicationPackageDownloadUrl,
                    availableUpdate.DisplayVersion,
                    availableUpdate.ApplicationPackageSha256,
                    isUpdaterUpdate: false);
            }
            else
            {
                // No app package in the manifest - nothing left to do for this version.
                return new UpdateCheckResult(false, currentInstallation, availableUpdate);
            }
        }

        if (currentInstallation != null && availableUpdate.Version.CompareTo(currentInstallation.Version) <= 0)
        {
            return new UpdateCheckResult(false, currentInstallation, availableUpdate);
        }

        var skippedVersion = ReadVersionHint(_skippedVersionFilePath);
        if (skippedVersion != null && skippedVersion.CompareTo(availableUpdate.Version) == 0)
        {
            return new UpdateCheckResult(false, currentInstallation, availableUpdate, false, true);
        }

        var localMatch = GetInstalledVersions().FirstOrDefault(installation => installation.Version.CompareTo(availableUpdate.Version) == 0);
        return new UpdateCheckResult(true, localMatch ?? currentInstallation, availableUpdate, localMatch != null);
    }

    public async Task<LocalApplicationInstallation> DownloadAndInstallUpdateAsync(AvailableUpdate update, IProgress<InstallationProgress>? progress, CancellationToken ct)
    {
        if (update.IsUpdaterUpdate)
        {
            await DownloadAndExecuteUpdaterUpdateAsync(update, progress, ct).ConfigureAwait(false);
            throw new SelfUpdateRestartRequiredException();
        }

        var existingInstallation = GetInstalledVersions().FirstOrDefault(installation => installation.Version.CompareTo(update.Version) == 0);
        if (existingInstallation != null)
        {
            PersistVersionMarkers(existingInstallation.Version.NormalizedValue);
            ClearSkippedVersion();
            return existingInstallation;
        }

        var archiveExtension   = GetArchiveExtension(update.DownloadUrl);
        var tempArchivePath    = Path.Combine(Path.GetTempPath(), "kit-"      + Guid.NewGuid().ToString("N") + archiveExtension);
        var stagingRoot        = Path.Combine(_baseDirectory, ".kit-staging-" + Guid.NewGuid().ToString("N"));
        var extractedDirectory = Path.Combine(stagingRoot, "extracted");
        var preparedDirectory  = Path.Combine(stagingRoot, "prepared");
        var targetDirectory    = Path.Combine(_baseDirectory, "app-" + update.Version.NormalizedValue);

        try
        {
            Report(progress, InstallationPhase.Downloading, update.Version.NormalizedValue);

            await DownloadFileAsync(update.DownloadUrl, tempArchivePath, update.Version.NormalizedValue, progress, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.VerifyingIntegrity, update.Version.NormalizedValue);

            await VerifyIntegrityAsync(update, tempArchivePath, ct).ConfigureAwait(false);

            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(extractedDirectory);

            Report(progress, InstallationPhase.ExtractingArchive, update.Version.NormalizedValue);

            await ExtractArchiveAsync(tempArchivePath, archiveExtension, extractedDirectory, ct).ConfigureAwait(false);

            if (_configuration.Installation.CompressFiles)
            {
                Report(progress, InstallationPhase.CompressingFiles, update.Version.NormalizedValue);
                NtfsCompressor.CompressDirectoryRecursive(extractedDirectory);
            }

            Report(progress, InstallationPhase.PreparingFiles, update.Version.NormalizedValue);

            PrepareExtractedFiles(extractedDirectory, preparedDirectory);

            Report(progress, InstallationPhase.ValidatingInstallation, update.Version.NormalizedValue);

            ValidatePreparedInstallation(preparedDirectory);

            Report(progress, InstallationPhase.RunningPostInstall, update.Version.NormalizedValue);

            await RunPostInstallAsync(preparedDirectory, update.Version.NormalizedValue, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.ValidatingInstallation, update.Version.NormalizedValue);

            ValidatePreparedInstallation(preparedDirectory);

            Report(progress, InstallationPhase.FinalizingInstallation, update.Version.NormalizedValue);

            if (Directory.Exists(targetDirectory))
            {
                throw new InvalidOperationException("The target app folder already exists: " + targetDirectory);
            }

            Directory.Move(preparedDirectory, targetDirectory);
            PersistVersionMarkers(update.Version.NormalizedValue);
            ClearSkippedVersion();
            CleanupOldVersions(targetDirectory);
            return BuildInstallation(update.Version.NormalizedValue);
        }
        finally
        {
            Report(progress, InstallationPhase.CleaningUp, update.Version.NormalizedValue);
            TryDeleteFile(tempArchivePath);
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task DownloadAndExecuteUpdaterUpdateAsync(AvailableUpdate update, IProgress<InstallationProgress>? progress, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "kit-installer-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            Report(progress, InstallationPhase.Downloading, update.Version.NormalizedValue);
            await DownloadFileAsync(update.DownloadUrl, tempPath, update.Version.NormalizedValue, progress, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.VerifyingIntegrity, update.Version.NormalizedValue);
            await VerifyIntegrityAsync(update, tempPath, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.FinalizingInstallation, update.Version.NormalizedValue);
            var startInfo = new ProcessStartInfo
            {
                FileName        = tempPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public void SkipVersion(string version)
    {
        File.WriteAllText(_skippedVersionFilePath, version);
    }

    public bool IsApplicationRunning()
    {
        var processName = ResolveProcessName();
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(processName);
            return processes.Any(process => process.Id != Process.GetCurrentProcess().Id);
        }
        finally
        {
            if (processes != null)
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }

    public string GetApplicationProcessName() => ResolveProcessName();

    public void Launch(LocalApplicationInstallation installation)
    {
        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException("The configured application executable was not found.", installation.ExecutablePath);
        }

        PersistCurrentVersion(installation.Version.NormalizedValue);

        var arguments = BuildLaunchArguments();
        var startInfo = new ProcessStartInfo
        {
            FileName         = installation.ExecutablePath,
            WorkingDirectory = installation.DirectoryPath,
            UseShellExecute  = true,
            Arguments        = arguments
        };

        Process.Start(startInfo);
    }

    private List<LocalApplicationInstallation> GetInstalledVersions()
    {
        var installations = new List<LocalApplicationInstallation>();
        foreach (var directory in Directory.GetDirectories(_baseDirectory, "app-*", SearchOption.TopDirectoryOnly))
        {
            var folderName = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(folderName) || folderName.Length <= 4)
            {
                continue;
            }

            var versionText = folderName.Substring(4);
            if (!ApplicationVersion.TryParse(versionText, out var version))
            {
                continue;
            }

            installations.Add(new LocalApplicationInstallation(version!, directory, Path.Combine(directory, _configuration.LaunchExecutable)));
        }

        return installations;
    }

    private LocalApplicationInstallation BuildInstallation(string versionText)
    {
        if (!ApplicationVersion.TryParse(versionText, out var version))
        {
            throw new InvalidOperationException("Invalid application version: " + versionText);
        }

        var resolvedVersion = version!;
        var directory       = Path.Combine(_baseDirectory, "app-" + resolvedVersion.NormalizedValue);

        return new LocalApplicationInstallation(resolvedVersion, directory, Path.Combine(directory, _configuration.LaunchExecutable));
    }

    private ApplicationVersion? ReadVersionHint(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var raw = File.ReadAllText(path).Trim();
        return ApplicationVersion.TryParse(raw, out var version) ? version : null;
    }

    private ApplicationVersion? ReadConfiguredInitialVersion()
        => ApplicationVersion.TryParse(_configuration.InitialVersion, out var version) ? version : null;

    private string BuildLaunchArguments()
    {
        var inheritedArguments = Environment.GetCommandLineArgs().Skip(1).Select(QuoteArgument);
        var configuredArguments = string.IsNullOrWhiteSpace(_configuration.LaunchArguments)
            ? string.Empty
            : _configuration.LaunchArguments.Trim();

        var combined = string.Join(" ", inheritedArguments.Where(argument => argument.Length > 0));
        if (string.IsNullOrWhiteSpace(configuredArguments))
        {
            return combined;
        }

        if (string.IsNullOrWhiteSpace(combined))
        {
            return configuredArguments;
        }

        return configuredArguments + " " + combined;
    }

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        return argument.IndexOfAny([' ', '\t', '"']) >= 0
            ? "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : argument;
    }

    private async Task DownloadFileAsync(string url, string targetPath, string version, IProgress<InstallationProgress>? progress, CancellationToken ct)
    {
        using (var response = await UpdaterHttpClient.Shared.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
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
                    progress?.Report(new InstallationProgress(InstallationPhase.Downloading, version, totalRead, contentLength));
                }
            }
        }
    }

    private async Task VerifyIntegrityAsync(AvailableUpdate update, string archivePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(update.Sha256))
        {
            if (_configuration.Installation.RequireIntegrityVerification)
            {
                throw new InvalidOperationException("Integrity verification is required, but the update source did not provide a SHA-256 checksum.");
            }

            return;
        }

        string actualHash;
        using (var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var algorithm = SHA256.Create())
        {
            var hashBytes = await Task.Run(() => algorithm.ComputeHash(stream), ct).ConfigureAwait(false);
            actualHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        if (!string.Equals(UpdateSourceParsing.NormalizeSha256(actualHash), UpdateSourceParsing.NormalizeSha256(update.Sha256), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update failed integrity verification. The SHA-256 checksum did not match.");
        }
    }

    private async Task ExtractArchiveAsync(string archivePath, string archiveExtension, string destinationDirectory, CancellationToken ct)
    {
        var sevenZipPath = Path.Combine(_baseDirectory, "bin", "7za.exe");
        var hasSevenZip  = File.Exists(sevenZipPath);
        if (hasSevenZip)
        {
            await ExtractWithSevenZipAsync(sevenZipPath, archivePath, destinationDirectory, ct).ConfigureAwait(false);
            return;
        }

        if (string.Equals(archiveExtension, ".7z", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update is a .7z archive, but no extractor was found. Ship bin\\7za.exe next to the updater to enable .7z extraction.");
        }

        if (!string.Equals(archiveExtension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported update archive format: " + archiveExtension + ". Ship bin\\7za.exe next to the updater to enable external extraction.");
        }

        ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
    }

    private void PrepareExtractedFiles(string extractedDirectory, string preparedDirectory)
    {
        var sourceDirectory = ResolvePreparedSourceDirectory(extractedDirectory);
        Directory.Move(sourceDirectory, preparedDirectory);
    }

    private string ResolvePreparedSourceDirectory(string extractedDirectory)
    {
        var layout                 = _configuration.Installation.ExtractionLayout.Trim().ToLowerInvariant();
        var childDirectories       = Directory.GetDirectories(extractedDirectory);
        var childFiles             = Directory.GetFiles(extractedDirectory);
        var singleRootDirectory    = childDirectories.Length == 1 && childFiles.Length == 0 ? childDirectories[0] : null;
        var launchPathAtRoot       = Path.Combine(extractedDirectory, _configuration.LaunchExecutable);
        var launchPathAtSingleRoot = singleRootDirectory == null ? null : Path.Combine(singleRootDirectory, _configuration.LaunchExecutable);

        switch (layout)
        {
            case "":
            case "auto":
                if (File.Exists(launchPathAtRoot))
                {
                    return extractedDirectory;
                }

                if (singleRootDirectory != null && File.Exists(launchPathAtSingleRoot) || singleRootDirectory != null)
                {
                    return singleRootDirectory;
                }

                return extractedDirectory;
            case "direct":
                return extractedDirectory;
            case "strip-single-root-directory":
                if (singleRootDirectory == null)
                {
                    throw new InvalidOperationException("Extraction layout is set to strip-single-root-directory, but the archive did not contain exactly one top-level directory.");
                }

                return singleRootDirectory;
            default:
                throw new InvalidOperationException("Unsupported extractionLayout value: " + _configuration.Installation.ExtractionLayout);
        }
    }

    private void ValidatePreparedInstallation(string preparedDirectory)
    {
        if (!Directory.Exists(preparedDirectory))
        {
            throw new InvalidOperationException("The prepared installation directory was not created.");
        }

        var executablePath = Path.Combine(preparedDirectory, _configuration.LaunchExecutable);
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException("The extracted update does not contain the configured launch executable: " + _configuration.LaunchExecutable);
        }
    }

    private async Task RunPostInstallAsync(string preparedDirectory, string version, CancellationToken cancellationToken)
    {
        var command = _configuration.Installation.PostInstallCommand.Trim();
        if (command.Length == 0)
        {
            return;
        }

        command = ReplaceTemplateTokens(command, version, preparedDirectory);

        var arguments = ReplaceTemplateTokens(_configuration.Installation.PostInstallArguments, version, preparedDirectory);

        ResolvePostInstallCommand(preparedDirectory, command, out var resolvedCommandPath, out var useCommandShell);

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            WorkingDirectory       = preparedDirectory
        };

        if (useCommandShell)
        {
            startInfo.FileName  = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/c \"" + resolvedCommandPath + (string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments) + "\"";
        }
        else
        {
            startInfo.FileName  = resolvedCommandPath;
            startInfo.Arguments = arguments;
        }

        using (var process = new Process())
        {
            process.StartInfo           = startInfo;
            process.EnableRaisingEvents = true;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask  = process.StandardError.ReadToEndAsync();

            using (cancellationToken.Register(() =>
                   {
                       try
                       {
                           if (!process.HasExited)
                           {
                               process.Kill();
                           }
                       }
                       catch
                       {
                           /*Ignored*/
                       }
                   }))
            {
                await WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            }

            var output = await outputTask.ConfigureAwait(false);
            var error  = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new InvalidOperationException("The post-install command failed." + (string.IsNullOrWhiteSpace(details) ? string.Empty : Environment.NewLine + details.Trim()));
            }
        }
    }

    private void ResolvePostInstallCommand(string preparedDirectory, string command, out string resolvedCommandPath, out bool useCommandShell)
    {
        useCommandShell     = false;
        resolvedCommandPath = command;

        if (!Path.IsPathRooted(resolvedCommandPath))
        {
            var appRelativePath = Path.Combine(preparedDirectory, resolvedCommandPath);
            if (File.Exists(appRelativePath))
            {
                resolvedCommandPath = appRelativePath;
            }
            else
            {
                var baseRelativePath = Path.Combine(_baseDirectory, resolvedCommandPath);
                if (File.Exists(baseRelativePath))
                {
                    resolvedCommandPath = baseRelativePath;
                }
            }
        }

        var extension = Path.GetExtension(resolvedCommandPath);
        if (
            string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
        )
        {
            useCommandShell = true;
        }
    }

    private void CleanupOldVersions(string currentTargetDirectory)
    {
        var keepLastVersions = _configuration.Installation.KeepLastVersions;
        if (keepLastVersions <= 0)
        {
            return;
        }

        var installationsToDelete = GetInstalledVersions()
                                    .OrderByDescending(installation => installation.Version)
                                    .Skip(keepLastVersions)
                                    .Where(installation => !string.Equals(installation.DirectoryPath, currentTargetDirectory, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

        foreach (var installation in installationsToDelete)
        {
            TryDeleteDirectory(installation.DirectoryPath);
        }
    }

    private string ResolveProcessName()
    {
        var configuredProcessName = _configuration.Installation.ProcessName.Trim();
        if (configuredProcessName.Length > 0)
        {
            return Path.GetFileNameWithoutExtension(configuredProcessName);
        }

        return Path.GetFileNameWithoutExtension(_configuration.LaunchExecutable);
    }

    private static async Task ExtractWithSevenZipAsync(string sevenZipPath, string archivePath, string destinationDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName               = sevenZipPath,
            Arguments              = "x -y -o\"" + destinationDirectory + "\" \"" + archivePath + "\"",
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };

        using (var process = new Process())
        {
            process.StartInfo           = startInfo;
            process.EnableRaisingEvents = true;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask  = process.StandardError.ReadToEndAsync();
            using (ct.Register(() =>
                   {
                       try
                       {
                           if (!process.HasExited)
                           {
                               process.Kill();
                           }
                       }
                       catch
                       {
                           /*Ignored*/
                       }
                   }))
            {
                await WaitForExitAsync(process, ct).ConfigureAwait(false);
            }

            var output = await outputTask.ConfigureAwait(false);
            var error  = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new InvalidOperationException("7za.exe failed to extract the update archive." + (string.IsNullOrWhiteSpace(details) ? string.Empty : Environment.NewLine + details.Trim()));
            }
        }
    }

    private static Task WaitForExitAsync(Process process, CancellationToken ct)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();

        EventHandler handler = delegate
        {
            taskCompletionSource.TrySetResult(true);
        };

        process.Exited += handler;

        if (process.HasExited)
        {
            process.Exited -= handler;
            return Task.CompletedTask;
        }

        ct.Register(() => taskCompletionSource.TrySetCanceled(ct));

        return taskCompletionSource.Task.ContinueWith(task =>
        {
            process.Exited -= handler;
            return task;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
    }

    private static string GetArchiveExtension(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            return ".zip";
        }

        var extension = Path.GetExtension(uri.AbsolutePath);

        return string.IsNullOrWhiteSpace(extension) ? ".zip" : extension;
    }

    private void PersistVersionMarkers(string version)
    {
        PersistCurrentVersion(version);
        File.WriteAllText(_lastDownloadedVersionFilePath, version);
    }

    private void PersistCurrentVersion(string version)
    {
        File.WriteAllText(_currentVersionFilePath, version);
    }

    private void ClearSkippedVersion()
    {
        TryDeleteFile(_skippedVersionFilePath);
    }

    private static string ReplaceTemplateTokens(string? value, string version, string appDirectory)
        => (value ?? string.Empty)
           .Replace("{Version}", version)
           .Replace("{AppDirectory}", appDirectory)
           .Replace("{BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);

    private static void Report(IProgress<InstallationProgress>? progress, InstallationPhase phase, string version)
    {
        progress?.Report(new InstallationProgress(phase, version, null, null));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            /*Ignored*/
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            /*Ignored*/
        }
    }
}
