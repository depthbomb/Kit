using Shared;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Kit.Cli;

internal static class ReleaseManifestBuilder
{
    public static ReleaseManifest Build(string version,
                                        string updaterPath,
                                        string packagePath,
                                        string? installerPath,
                                        bool updaterUpdateRequired,
                                        string? deltaFromVersion = null,
                                        string? deltaPackagePath = null,
                                        string? deltaDeleteListPath = null)
    {
        if (!StampVersion.TryParse(version))
        {
            throw new InvalidOperationException("The release version is not a valid version string.");
        }

        var payloadJson = StampPayload.ReadConfigurationJson(updaterPath);
        var payload = JsonSerializer.Deserialize<UpdaterConfiguration>(payloadJson, new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                      })
                      ?? throw new InvalidOperationException("The updater does not contain a valid stamped payload.");

        var applicationPackage = BuildPackageReference(packagePath, "application");

        if (updaterUpdateRequired && !VersionsMatch(payload.UpdaterVersion, version))
        {
            throw new InvalidOperationException(
                "The updater must be stamped with --version matching the manifest version when an updater update is required.");
        }

        ReleasePackageReference updaterPackage;
        if (string.IsNullOrEmpty(installerPath))
        {
            if (updaterUpdateRequired)
            {
                throw new InvalidOperationException("An installer path must be provided when an updater update is required.");
            }

            updaterPackage = new ReleasePackageReference();
        }
        else
        {
            updaterPackage = BuildPackageReference(installerPath, "installer");
        }

        var selectedPackage = updaterUpdateRequired ? updaterPackage : applicationPackage;
        var deltaPackages = BuildDeltaPackages(deltaFromVersion, deltaPackagePath, deltaDeleteListPath);

        return new ReleaseManifest
        {
            ApplicationName       = payload.ApplicationName,
            Version               = version,
            Channel               = string.IsNullOrWhiteSpace(payload.UpdateSource.Channel) ? "stable" : payload.UpdateSource.Channel.Trim(),
            UpdaterUpdateRequired = updaterUpdateRequired,
            Download = new ReleaseDownloadInstruction
            {
                Kind     = selectedPackage.Kind,
                FileName = selectedPackage.FileName,
                Sha256   = selectedPackage.Sha256
            },
            ApplicationPackage = applicationPackage,
            UpdaterPackage     = updaterPackage,
            DeltaPackages      = deltaPackages
        };
    }

    private static List<ReleaseDeltaPackageReference> BuildDeltaPackages(string? fromVersion, string? packagePath, string? deleteListPath)
    {
        var hasVersion = !string.IsNullOrWhiteSpace(fromVersion);
        var hasPackage = !string.IsNullOrWhiteSpace(packagePath);
        if (hasVersion != hasPackage)
        {
            throw new InvalidOperationException("A delta package requires both --delta-from-version and --delta-package.");
        }

        if (!hasVersion)
        {
            if (!string.IsNullOrWhiteSpace(deleteListPath))
            {
                throw new InvalidOperationException("--delta-delete-list requires a delta package.");
            }

            return [];
        }

        if (!StampVersion.TryParse(fromVersion!) || !File.Exists(packagePath!))
        {
            throw new InvalidOperationException("The delta base version or package is invalid.");
        }

        var deletedFiles = string.IsNullOrWhiteSpace(deleteListPath)
            ? []
            : File.ReadAllLines(deleteListPath!)
                  .Select(value => value.Trim())
                  .Where(value => value.Length > 0 && !value.StartsWith("#", StringComparison.Ordinal))
                  .ToList();

        foreach (var path in deletedFiles)
        {
            if (Path.IsPathRooted(path) || path.Split('/', '\\').Any(segment => segment == ".."))
            {
                throw new InvalidOperationException("The delta deletion list contains an unsafe path: " + path);
            }
        }

        return
        [
            new ReleaseDeltaPackageReference
            {
                FromVersion = fromVersion!.Trim(),
                FileName = Path.GetFileName(packagePath),
                Sha256 = ComputeSha256(packagePath!),
                DeletedFiles = deletedFiles
            }
        ];
    }

    private static ReleasePackageReference BuildPackageReference(string path, string kind) => new()
    {
        Kind     = kind,
        FileName = Path.GetFileName(path),
        Sha256   = ComputeSha256(path),
        Files    = IsZipArchive(path) ? BuildFileReferences(path) : []
    };

    private static bool IsZipArchive(string path) => string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);

    private static bool VersionsMatch(string? stampedVersion, string? manifestVersion)
        => string.Equals(RemoveVersionPrefix(stampedVersion), RemoveVersionPrefix(manifestVersion), StringComparison.OrdinalIgnoreCase);

    private static string RemoveVersionPrefix(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1 && char.IsDigit(trimmed[1])
            ? trimmed.Substring(1)
            : trimmed;
    }

    private static List<ReleasePackageFileReference> BuildFileReferences(string zipPath)
    {
        var files = new List<ReleasePackageFileReference>();

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var algorithm   = SHA512.Create();
            var hashBytes         = algorithm.ComputeHash(entryStream);

            files.Add(new ReleasePackageFileReference
            {
                Path   = entry.FullName,
                Sha512 = Convert.ToHexString(hashBytes).ToLowerInvariant(),
                Size   = entry.Length
            });
        }

        files.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
        return files;
    }

    private static string ComputeSha256(string path)
    {
        using var stream    = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var algorithm = SHA256.Create();

        var hashBytes = algorithm.ComputeHash(stream);

        return Convert.ToHexString(hashBytes);
    }
}
