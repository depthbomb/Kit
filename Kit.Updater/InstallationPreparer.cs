using Shared;
using System.Security.Cryptography;

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

    public void VerifyPostExtractIntegrity(string extractedDirectory, IEnumerable<ReleasePackageFileReference>? files)
    {
        if (!_configuration.Installation.RequireIntegrityVerification)
        {
            return;
        }

        var fileEntries = files?.Where(file => !string.IsNullOrWhiteSpace(file.Path)).ToArray();
        if (fileEntries == null || fileEntries.Length == 0)
        {
            throw new InvalidOperationException(
                "Post-extraction integrity verification is required, but the release manifest did not provide file integrity data.");
        }

        // Determine the root directory that will become the app folder after PrepareExtractedFiles.
        // Manifest entry paths are relative to the ZIP root, so if a single root directory is being
        // stripped we need to strip that same prefix from each path before resolving files on disk.
        var sourceDirectory = ResolvePreparedSourceDirectory(extractedDirectory);
        var stripPrefix = !string.Equals(sourceDirectory, extractedDirectory, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(sourceDirectory) + Path.DirectorySeparatorChar
            : null;
        var sourceDirectoryRoot = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var fileEntry in fileEntries)
        {
            var rawPath = fileEntry.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(rawPath))
            {
                throw new InvalidOperationException("Post-extraction verification failed. The release manifest contains an empty file path.");
            }

            if (Path.IsPathRooted(rawPath))
            {
                throw new InvalidOperationException($"Post-extraction verification failed. The release manifest contains an invalid rooted path: {fileEntry.Path}");
            }

            var relativePath = stripPrefix != null && rawPath.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)
                ? rawPath.Substring(stripPrefix.Length)
                : rawPath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException("Post-extraction verification failed. The release manifest contains an empty file path after path normalization.");
            }

            var filePath = Path.GetFullPath(Path.Combine(sourceDirectory, relativePath));
            if (!filePath.StartsWith(sourceDirectoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Post-extraction verification failed. The release manifest contains an invalid path outside the extracted directory: {fileEntry.Path}");
            }

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"Post-extraction verification failed. Required file is missing: {relativePath}");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != fileEntry.Size)
            {
                throw new InvalidOperationException($"Post-extraction verification failed for {relativePath}. File size mismatch.");
            }

            var actualHash = ComputeSha512(filePath);
            if (!string.Equals(actualHash, NormalizeHex(fileEntry.Sha512), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Post-extraction verification failed for {relativePath}. SHA-512 checksum mismatch.");
            }
        }
    }

    private string ResolvePreparedSourceDirectory(string extractedDirectory)
    {
        var layout           = _configuration.Installation.ExtractionLayout.Trim().ToLowerInvariant();
        var childDirectories = Directory.GetDirectories(extractedDirectory);

        // Exclude the manifest from the root file count so it does not prevent single-root detection.
        var childFiles = Directory.GetFiles(extractedDirectory)
                                  .Where(f => !string.Equals(Path.GetFileName(f) ?? string.Empty, ".kit-files-manifest.json", StringComparison.OrdinalIgnoreCase))
                                  .ToArray();

        var singleRootDirectory = childDirectories.Length == 1 && childFiles.Length == 0 ? childDirectories[0] : null;
        var launchPathAtRoot    = Path.Combine(extractedDirectory, _configuration.LaunchExecutable);

        switch (layout)
        {
            case "":
            case "auto":
                if (File.Exists(launchPathAtRoot))
                {
                    return extractedDirectory;
                }

                if (singleRootDirectory != null)
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

                return singleRootDirectory!;
            default:
                throw new InvalidOperationException("Unsupported extractionLayout value: " + _configuration.Installation.ExtractionLayout);
        }
    }

    private static string ComputeSha512(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha512 = SHA512.Create();

        var hashBytes = sha512.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string NormalizeHex(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();
}
