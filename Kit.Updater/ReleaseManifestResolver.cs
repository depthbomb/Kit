using Shared;

namespace Kit.Updater;

internal static class ReleaseManifestResolver
{
    public static AvailableUpdate ResolveAvailableUpdate(ReleaseManifest      manifest,
                                                         string               expectedApplicationName,
                                                         string               expectedChannel,
                                                         Func<string, string?> resolveFileUrl)
    {
        if (string.IsNullOrWhiteSpace(manifest.ApplicationName)
            || !string.Equals(manifest.ApplicationName.Trim(), expectedApplicationName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The release manifest application name does not match this updater.");
        }

        if (!UpdateChannel.Matches(expectedChannel, manifest.Channel))
        {
            throw new InvalidOperationException("The release manifest channel does not match this updater.");
        }

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
        if (isUpdaterUpdate)
        {
            if (manifest.ApplicationPackage == null || string.IsNullOrWhiteSpace(manifest.ApplicationPackage.FileName))
            {
                throw new InvalidOperationException("The release manifest must provide an application package when an updater update is required.");
            }

            appPackageUrl    = resolveFileUrl(manifest.ApplicationPackage.FileName);
            appPackageSha256 = manifest.ApplicationPackage.Sha256;
            if (string.IsNullOrWhiteSpace(appPackageUrl))
            {
                throw new InvalidOperationException("The release manifest application package target could not be resolved.");
            }
        }

        return new AvailableUpdate(
            resolvedVersion,
            downloadUrl!,
            resolvedVersion.NormalizedValue,
            manifest.Download.Sha256,
            isUpdaterUpdate,
            appPackageUrl,
            appPackageSha256,
            manifest.ApplicationPackage.Files);
    }
}
