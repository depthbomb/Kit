using Shared;

namespace Kit.Updater;

internal sealed class InstallationStateStore
{
    private const string CurrentVersionFileName        = ".kit-current-version";
    private const string LastDownloadedVersionFileName = ".kit-last-downloaded-version";
    private const string SkippedVersionFileName        = ".kit-skipped-version";

    private readonly UpdaterConfiguration _configuration;
    private readonly string               _baseDirectory;
    private readonly string               _currentVersionFilePath;
    private readonly string               _lastDownloadedVersionFilePath;
    private readonly string               _skippedVersionFilePath;

    private List<LocalApplicationInstallation>? _installedVersionsCache;
    private bool                                _installedVersionsDirty = true;

    public InstallationStateStore(UpdaterConfiguration configuration, string baseDirectory)
    {
        _configuration                 = configuration;
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

    public LocalApplicationInstallation? FindInstalledVersion(ApplicationVersion version)
        => GetInstalledVersions().FirstOrDefault(installation => installation.Version.CompareTo(version) == 0);

    public ApplicationVersion? ReadSkippedVersion()
        => ReadVersionHint(_skippedVersionFilePath);

    public void SkipVersion(string version)
    {
        File.WriteAllText(_skippedVersionFilePath, version);
    }

    public void PersistVersionMarkers(string version)
    {
        PersistCurrentVersion(version);
        File.WriteAllText(_lastDownloadedVersionFilePath, version);
    }

    public void PersistCurrentVersion(string version)
    {
        File.WriteAllText(_currentVersionFilePath, version);
    }

    public void ClearSkippedVersion()
    {
        TryDeleteFile(_skippedVersionFilePath);
    }

    public LocalApplicationInstallation BuildInstallation(string versionText)
    {
        if (!ApplicationVersion.TryParse(versionText, out var version))
        {
            throw new InvalidOperationException("Invalid application version: " + versionText);
        }

        var resolvedVersion = version!;
        var directory       = ResolveVersionDirectory(resolvedVersion);

        return new LocalApplicationInstallation(resolvedVersion, directory, Path.Combine(directory, _configuration.LaunchExecutable));
    }

    public string ResolveVersionDirectory(ApplicationVersion version)
    {
        var baseDirectory = Path.GetFullPath(_baseDirectory)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directory = Path.GetFullPath(Path.Combine(baseDirectory, "app-" + version.NormalizedValue));
        var parentDirectory = Path.GetDirectoryName(directory);

        if (!string.Equals(parentDirectory, baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The application version resolves outside the updater directory.");
        }

        return directory;
    }

    public void MarkInstalledVersionsDirty()
    {
        _installedVersionsDirty = true;
        _installedVersionsCache = null;
    }

    public void CleanupOldVersions(string currentTargetDirectory)
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

        if (installationsToDelete.Count > 0)
        {
            MarkInstalledVersionsDirty();
        }
    }

    public string ResolveProcessName()
    {
        var configuredProcessName = _configuration.Installation.ProcessName.Trim();
        if (configuredProcessName.Length > 0)
        {
            return Path.GetFileNameWithoutExtension(configuredProcessName);
        }

        return Path.GetFileNameWithoutExtension(_configuration.LaunchExecutable);
    }

    private List<LocalApplicationInstallation> GetInstalledVersions()
    {
        if (!_installedVersionsDirty && _installedVersionsCache != null)
        {
            return _installedVersionsCache;
        }

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

        _installedVersionsCache = installations;
        _installedVersionsDirty = false;
        return installations;
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
