using Shared;
using System.Security.Cryptography;
using System.Text;

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
    public AvailableUpdate(ApplicationVersion                          version,
                           string                                      downloadUrl,
                           string                                      displayVersion,
                           string                                      sha256,
                           bool                                        isUpdaterUpdate               = false,
                           string?                                     applicationPackageDownloadUrl = null,
                           string?                                     applicationPackageSha256      = null,
                           IReadOnlyList<ReleasePackageFileReference>? applicationPackageFiles       = null,
                           IReadOnlyList<AvailableDeltaPackage>?       deltaPackages                  = null,
                           string?                                     deltaBaseDirectory             = null,
                           IReadOnlyList<string>?                      deltaDeletedFiles              = null)
    {
        Version                       = version;
        DownloadUrl                   = downloadUrl;
        DisplayVersion                = displayVersion;
        Sha256                        = sha256;
        IsUpdaterUpdate               = isUpdaterUpdate;
        ApplicationPackageDownloadUrl = applicationPackageDownloadUrl      ?? string.Empty;
        ApplicationPackageSha256      = applicationPackageSha256           ?? string.Empty;
        ApplicationPackageFiles       = applicationPackageFiles?.ToArray() ?? [];
        DeltaPackages                 = deltaPackages?.ToArray() ?? [];
        DeltaBaseDirectory            = deltaBaseDirectory ?? string.Empty;
        DeltaDeletedFiles             = deltaDeletedFiles?.ToArray() ?? [];
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

    /// <summary>The per-file integrity metadata for the app package ZIP.</summary>
    public IReadOnlyList<ReleasePackageFileReference> ApplicationPackageFiles { get; private set; }

    public IReadOnlyList<AvailableDeltaPackage> DeltaPackages { get; private set; }

    public string DeltaBaseDirectory { get; private set; }

    public IReadOnlyList<string> DeltaDeletedFiles { get; private set; }

    public bool IsDelta => !string.IsNullOrWhiteSpace(DeltaBaseDirectory);
}

internal sealed class AvailableDeltaPackage
{
    public AvailableDeltaPackage(ApplicationVersion fromVersion, string downloadUrl, string sha256, IReadOnlyList<string>? deletedFiles)
    {
        FromVersion = fromVersion;
        DownloadUrl = downloadUrl;
        Sha256 = sha256;
        DeletedFiles = deletedFiles?.ToArray() ?? [];
    }

    public ApplicationVersion FromVersion { get; }
    public string DownloadUrl { get; }
    public string Sha256 { get; }
    public IReadOnlyList<string> DeletedFiles { get; }
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
    InstallingAppRuntime,
    DownloadingWebView2Runtime,
    InstallingWebView2Runtime
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
    private readonly UpdaterConfiguration   _configuration;
    private readonly IUpdateSource          _updateSource;
    private readonly string                 _baseDirectory;
    private readonly InstallationStateStore _installationState;
    private readonly UpdateDownloader       _downloader;
    private readonly ArchiveExtractor       _archiveExtractor;
    private readonly InstallationPreparer   _installationPreparer;
    private readonly PostInstallRunner      _postInstallRunner;
    private readonly ApplicationLauncher    _applicationLauncher;

    public UpdaterRuntime(UpdaterConfiguration configuration, string baseDirectory)
    {
        _configuration        = configuration;
        _updateSource         = UpdateSourceFactory.Create(configuration);
        _baseDirectory        = baseDirectory;
        _installationState    = new InstallationStateStore(configuration, baseDirectory);
        _downloader           = new UpdateDownloader();
        _archiveExtractor     = new ArchiveExtractor(baseDirectory);
        _installationPreparer = new InstallationPreparer(configuration);
        _postInstallRunner    = new PostInstallRunner(configuration, baseDirectory);
        _applicationLauncher  = new ApplicationLauncher(configuration, _installationState);
    }

    public LocalApplicationInstallation? ResolveCurrentInstallation()
        => _installationState.ResolveCurrentInstallation();

    public async Task<UpdateCheckResult> CheckForUpdateAsync(LocalApplicationInstallation? currentInstallation,
                                                             CancellationToken             ct)
    {
        var availableUpdate = await _updateSource.GetAvailableUpdateAsync(ct).ConfigureAwait(false);
        return CheckForProvidedUpdate(currentInstallation, availableUpdate);
    }

    public UpdateCheckResult CheckForProvidedUpdate(LocalApplicationInstallation? currentInstallation,
                                                    AvailableUpdate?              availableUpdate)
    {
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
                    isUpdaterUpdate: false,
                    applicationPackageFiles: availableUpdate.ApplicationPackageFiles,
                    deltaPackages: availableUpdate.DeltaPackages);
            }
            else
            {
                // No app package in the manifest - nothing left to do for this version.
                return new UpdateCheckResult(false, currentInstallation, availableUpdate);
            }
        }

        if (!availableUpdate.IsUpdaterUpdate && currentInstallation != null)
        {
            var delta = availableUpdate.DeltaPackages.FirstOrDefault(candidate =>
                candidate.FromVersion.CompareTo(currentInstallation.Version) == 0);
            if (delta != null)
            {
                availableUpdate = new AvailableUpdate(
                    availableUpdate.Version,
                    delta.DownloadUrl,
                    availableUpdate.DisplayVersion,
                    delta.Sha256,
                    applicationPackageFiles: availableUpdate.ApplicationPackageFiles,
                    deltaBaseDirectory: currentInstallation.DirectoryPath,
                    deltaDeletedFiles: delta.DeletedFiles);
            }
        }

        if (currentInstallation != null && availableUpdate.Version.CompareTo(currentInstallation.Version) <= 0)
        {
            return new UpdateCheckResult(false, currentInstallation, availableUpdate);
        }

        // Do not honor a skipped-version marker for updater updates - they must always proceed.
        if (!availableUpdate.IsUpdaterUpdate && CanSkipUpdate(currentInstallation))
        {
            var skippedVersion = _installationState.ReadSkippedVersion();
            if (skippedVersion != null && skippedVersion.CompareTo(availableUpdate.Version) == 0)
            {
                return new UpdateCheckResult(false, currentInstallation, availableUpdate, false, true);
            }
        }

        var localMatch = _installationState.FindInstalledVersion(availableUpdate.Version);
        return new UpdateCheckResult(true, localMatch ?? currentInstallation, availableUpdate, localMatch != null);
    }

    public async Task<LocalApplicationInstallation> RepairInstallationAsync(LocalApplicationInstallation currentInstallation,
                                                                            AvailableUpdate              update,
                                                                            IProgress<InstallationProgress>? progress,
                                                                            CancellationToken             ct)
    {
        if (update.IsUpdaterUpdate || currentInstallation.Version.CompareTo(update.Version) != 0)
        {
            throw new InvalidOperationException("Repair requires an application package for the currently installed version.");
        }

        var originalDirectory = currentInstallation.DirectoryPath;
        var backupDirectory = Path.Combine(_baseDirectory, ".kit-repair-backup-" + Guid.NewGuid().ToString("N"));
        Directory.Move(originalDirectory, backupDirectory);
        _installationState.MarkInstalledVersionsDirty();

        try
        {
            var repaired = await DownloadAndInstallUpdateAsync(update, progress, ct).ConfigureAwait(false);
            TryDeleteDirectory(backupDirectory);
            return repaired;
        }
        catch
        {
            TryDeleteDirectory(originalDirectory);
            if (Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, originalDirectory);
            }

            _installationState.MarkInstalledVersionsDirty();
            throw;
        }
    }

    private bool CanSkipUpdate(LocalApplicationInstallation? currentInstallation)
    {
        var mode = _configuration.UpdatePolicy.Mode.Trim();
        if (mode.Length == 0 || string.Equals(mode, "optional", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(mode, "minimum-version-required", StringComparison.OrdinalIgnoreCase)
            || currentInstallation == null
            || !ApplicationVersion.TryParse(_configuration.UpdatePolicy.MinimumVersion, out var minimumVersion))
        {
            return false;
        }

        return currentInstallation.Version.CompareTo(minimumVersion) >= 0;
    }

    public async Task<LocalApplicationInstallation> DownloadAndInstallUpdateAsync(AvailableUpdate                  update,
                                                                                  IProgress<InstallationProgress>? progress,
                                                                                  CancellationToken                ct)
    {
        if (update.IsUpdaterUpdate)
        {
            await DownloadAndExecuteUpdaterUpdateAsync(update, progress, ct).ConfigureAwait(false);
            throw new SelfUpdateRestartRequiredException();
        }

        var existingInstallation = _installationState.FindInstalledVersion(update.Version);
        if (existingInstallation != null)
        {
            _installationState.PersistDownloadedVersion(existingInstallation.Version.NormalizedValue);
            _installationState.ClearSkippedVersion();
            return existingInstallation;
        }

        var archiveExtension   = ArchiveExtractor.GetArchiveExtension(update.DownloadUrl);
        var tempArchivePath    = BuildDownloadCachePath(update.DownloadUrl, archiveExtension);
        var stagingRoot        = Path.Combine(_baseDirectory, ".kit-staging-" + Guid.NewGuid().ToString("N"));
        var extractedDirectory = Path.Combine(stagingRoot, "extracted");
        var preparedDirectory  = Path.Combine(stagingRoot, "prepared");
        var targetDirectory    = _installationState.ResolveVersionDirectory(update.Version);

        var downloadVerified = false;
        try
        {
            Report(progress, InstallationPhase.Downloading, update.Version.NormalizedValue);

            await _downloader.DownloadFileAsync(update.DownloadUrl, tempArchivePath, update.Version.NormalizedValue, progress, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.VerifyingIntegrity, update.Version.NormalizedValue);

            try
            {
                await _downloader.VerifyIntegrityAsync(update, tempArchivePath, _configuration.Installation.RequireIntegrityVerification, ct).ConfigureAwait(false);
                downloadVerified = true;
            }
            catch (InvalidOperationException)
            {
                UpdateDownloader.DeleteDownloadFiles(tempArchivePath);
                throw;
            }

            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(extractedDirectory);

            Report(progress, InstallationPhase.ExtractingArchive, update.Version.NormalizedValue);

            await _archiveExtractor.ExtractAsync(tempArchivePath, archiveExtension, extractedDirectory, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.ValidatingInstallation, update.Version.NormalizedValue);

            Report(progress, InstallationPhase.PreparingFiles, update.Version.NormalizedValue);

            if (update.IsDelta)
            {
                DeltaInstallationBuilder.Build(update.DeltaBaseDirectory, extractedDirectory, preparedDirectory, update.DeltaDeletedFiles, ct);
                _installationPreparer.VerifyPostExtractIntegrity(preparedDirectory, update.ApplicationPackageFiles, ct);
            }
            else
            {
                _installationPreparer.VerifyPostExtractIntegrity(extractedDirectory, update.ApplicationPackageFiles, ct);
                _installationPreparer.PrepareExtractedFiles(extractedDirectory, preparedDirectory);
            }

            Report(progress, InstallationPhase.ValidatingInstallation, update.Version.NormalizedValue);

            _installationPreparer.ValidatePreparedInstallation(preparedDirectory);

            Report(progress, InstallationPhase.RunningPostInstall, update.Version.NormalizedValue);

            await _postInstallRunner.RunAsync(preparedDirectory, update.Version.NormalizedValue, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.ValidatingInstallation, update.Version.NormalizedValue);

            _installationPreparer.ValidatePreparedInstallation(preparedDirectory);
            _installationPreparer.CompressIfEnabled(preparedDirectory, update.Version.NormalizedValue, progress, ct);

            Report(progress, InstallationPhase.FinalizingInstallation, update.Version.NormalizedValue);

            if (Directory.Exists(targetDirectory))
            {
                throw new InvalidOperationException("The target app folder already exists: " + targetDirectory);
            }

            Directory.Move(preparedDirectory, targetDirectory);

            _installationState.MarkInstalledVersionsDirty();
            _installationState.PersistDownloadedVersion(update.Version.NormalizedValue);
            _installationState.ClearSkippedVersion();
            _installationState.CleanupOldVersions(targetDirectory);

            return _installationState.BuildInstallation(update.Version.NormalizedValue);
        }
        finally
        {
            Report(progress, InstallationPhase.CleaningUp, update.Version.NormalizedValue);
            if (downloadVerified)
            {
                UpdateDownloader.DeleteDownloadFiles(tempArchivePath);
            }
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task DownloadAndExecuteUpdaterUpdateAsync(AvailableUpdate                  update,
                                                            IProgress<InstallationProgress>? progress,
                                                            CancellationToken                ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "kit-installer-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            Report(progress, InstallationPhase.Downloading, update.Version.NormalizedValue);
            await _downloader.DownloadFileAsync(update.DownloadUrl, tempPath, update.Version.NormalizedValue, progress, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.VerifyingIntegrity, update.Version.NormalizedValue);
            await _downloader.VerifyIntegrityAsync(update, tempPath, _configuration.Installation.RequireIntegrityVerification, ct).ConfigureAwait(false);

            Report(progress, InstallationPhase.FinalizingInstallation, update.Version.NormalizedValue);
            _applicationLauncher.LaunchUpdaterInstaller(tempPath);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public void SkipVersion(string version) => _installationState.SkipVersion(version);

    public bool IsApplicationRunning() => _applicationLauncher.IsApplicationRunning();

    public string GetApplicationProcessName() => _applicationLauncher.GetApplicationProcessName();

    public void Launch(LocalApplicationInstallation installation) => _applicationLauncher.Launch(installation);

    public Task<bool> LaunchAndVerifyAsync(LocalApplicationInstallation  installation,
                                           LocalApplicationInstallation? previousInstallation,
                                           CancellationToken              ct)
        => _applicationLauncher.LaunchAndVerifyAsync(installation, previousInstallation, ct);

    private static void Report(IProgress<InstallationProgress>? progress, InstallationPhase phase, string version)
    {
        progress?.Report(new InstallationProgress(phase, version, null, null));
    }

    private string BuildDownloadCachePath(string downloadUrl, string archiveExtension)
    {
        byte[] hash;
        using (var algorithm = SHA256.Create())
        {
            hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(downloadUrl));
        }

        var fileName = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()
                       + archiveExtension
                       + ".partial";
        return Path.Combine(_baseDirectory, ".kit-downloads", fileName);
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
