using Shared;

namespace Kit.Updater;

internal static class ReleaseManifestResolver
{
    public static AvailableUpdate ResolveAvailableUpdate(ReleaseManifest manifest, Func<string, string?> resolveFileUrl)
    {
        if (!ApplicationVersion.TryParse(manifest.Version, out var version))
        {
            throw new InvalidOperationException("The release manifest did not provide a valid version.");
        }

        if (manifest.Download == null || string.IsNullOrWhiteSpace(manifest.Download.FileName))
        {
            throw new InvalidOperationException("The release manifest did not provide a download filename.");
        }

        var downloadUrl = resolveFileUrl(manifest.Download.FileName);
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("The release manifest download target could not be resolved.");
        }

        var resolvedVersion = version!;
        var isUpdaterUpdate = manifest.UpdaterUpdateRequired || string.Equals(manifest.Download.Kind, "installer", StringComparison.OrdinalIgnoreCase);

        string? appPackageUrl    = null;
        string? appPackageSha256 = null;
        if (isUpdaterUpdate && !string.IsNullOrWhiteSpace(manifest.ApplicationPackage.FileName))
        {
            appPackageUrl    = resolveFileUrl(manifest.ApplicationPackage.FileName);
            appPackageSha256 = manifest.ApplicationPackage.Sha256;
        }

        return new AvailableUpdate(
            resolvedVersion,
            downloadUrl!,
            resolvedVersion.NormalizedValue,
            manifest.Download.Sha256,
            isUpdaterUpdate,
            appPackageUrl,
            appPackageSha256);
    }
}
