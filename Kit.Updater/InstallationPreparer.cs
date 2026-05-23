using Shared;

namespace Kit.Updater;

internal sealed class InstallationPreparer
{
    private readonly UpdaterConfiguration _configuration;

    public InstallationPreparer(UpdaterConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void CompressIfEnabled(string extractedDirectory, string version, IProgress<InstallationProgress>? progress)
    {
        if (!_configuration.Installation.CompressFiles)
        {
            return;
        }

        progress?.Report(new InstallationProgress(InstallationPhase.CompressingFiles, version, null, null));
        NtfsCompressor.CompressDirectoryRecursive(extractedDirectory);
    }

    public void PrepareExtractedFiles(string extractedDirectory, string preparedDirectory)
    {
        var sourceDirectory = ResolvePreparedSourceDirectory(extractedDirectory);
        Directory.Move(sourceDirectory, preparedDirectory);
    }

    public void ValidatePreparedInstallation(string preparedDirectory)
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
}
