using Shared;
using System.Text.Json;
using System.Security.Cryptography;

namespace Kit.Cli;

internal static class ReleaseManifestBuilder
{
    public static ReleaseManifest Build(string version, string updaterPath, string packagePath, string? installerPath, bool updaterUpdateRequired)
    {
        var payloadJson = StampPayload.ReadConfigurationJson(updaterPath);
        var payload = JsonSerializer.Deserialize<UpdaterConfiguration>(payloadJson, new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                      })
                      ?? throw new InvalidOperationException("The updater does not contain a valid stamped payload.");

        var applicationPackage = BuildPackageReference(packagePath, "application");

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

        return new ReleaseManifest
        {
            ApplicationName       = payload.ApplicationName,
            Version               = version,
            UpdaterUpdateRequired = updaterUpdateRequired,
            Download = new ReleaseDownloadInstruction
            {
                Kind     = selectedPackage.Kind,
                FileName = selectedPackage.FileName,
                Sha256   = selectedPackage.Sha256
            },
            ApplicationPackage = applicationPackage,
            UpdaterPackage     = updaterPackage
        };
    }

    private static ReleasePackageReference BuildPackageReference(string path, string kind) => new()
    {
        Kind     = kind,
        FileName = Path.GetFileName(path),
        Sha256   = ComputeSha256(path)
    };

    private static string ComputeSha256(string path)
    {
        using var stream    = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var algorithm = SHA256.Create();

        var hashBytes = algorithm.ComputeHash(stream);

        return Convert.ToHexString(hashBytes);
    }
}
