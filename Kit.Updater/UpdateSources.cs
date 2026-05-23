using Shared;
using System.Web.Script.Serialization;

namespace Kit.Updater;

internal interface IUpdateSource
{
    Task<AvailableUpdate?> GetAvailableUpdateAsync(CancellationToken ct);
}

internal static class UpdateSourceFactory
{
    public static IUpdateSource Create(UpdaterConfiguration configuration)
    {
        var sourceType = configuration.UpdateSource.Type.Trim().ToLowerInvariant();
        return sourceType switch
        {
            "json"   => new JsonUpdateSource(configuration.UpdateSource),
            "github" => new GitHubUpdateSource(configuration.UpdateSource),
            _        => throw new InvalidOperationException("Unsupported update source type: " + configuration.UpdateSource.Type)
        };
    }
}

internal sealed class JsonUpdateSource : IUpdateSource
{
    private readonly UpdateSourceConfiguration _configuration;

    public JsonUpdateSource(UpdateSourceConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<AvailableUpdate?> GetAvailableUpdateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Url))
        {
            throw new InvalidOperationException("The JSON update source URL is not configured.");
        }

        var json       = await UpdateSourceHttp.GetStringAsync(_configuration.Url, ct).ConfigureAwait(false);
        var serializer = new JavaScriptSerializer();
        var manifest   = serializer.Deserialize<ReleaseManifest>(json);
        if (manifest == null)
        {
            throw new InvalidOperationException("The JSON update source returned an invalid or empty manifest.");
        }

        if (!ApplicationVersion.TryParse(manifest.Version, out var version))
        {
            throw new InvalidOperationException("The release manifest did not provide a valid version.");
        }

        if (manifest.Download == null || string.IsNullOrWhiteSpace(manifest.Download.FileName))
        {
            throw new InvalidOperationException("The release manifest did not provide a download filename.");
        }

        // For JSON sources, we assume the download URL is either absolute or relative to the manifest URL.
        var downloadUrl = manifest.Download.FileName;
        if (!Uri.IsWellFormedUriString(downloadUrl, UriKind.Absolute))
        {
            var baseUri = new Uri(_configuration.Url);
            downloadUrl = new Uri(baseUri, downloadUrl).ToString();
        }

        var resolvedVersion = version!;
        var isUpdaterUpdate = manifest.UpdaterUpdateRequired || string.Equals(manifest.Download.Kind, "installer", StringComparison.OrdinalIgnoreCase);

        // Resolve the application package as a fallback URL/hash for after an updater-installer update.
        string? appPackageUrl  = null;
        string? appPackageSha256 = null;
        if (isUpdaterUpdate && !string.IsNullOrWhiteSpace(manifest.ApplicationPackage.FileName))
        {
            var appPackageFileName = manifest.ApplicationPackage.FileName;
            if (Uri.IsWellFormedUriString(appPackageFileName, UriKind.Absolute))
            {
                appPackageUrl = appPackageFileName;
            }
            else
            {
                var baseUri = new Uri(_configuration.Url);
                appPackageUrl = new Uri(baseUri, appPackageFileName).ToString();
            }

            appPackageSha256 = manifest.ApplicationPackage.Sha256;
        }

        return new AvailableUpdate(resolvedVersion, downloadUrl, resolvedVersion.NormalizedValue, manifest.Download.Sha256, isUpdaterUpdate,
                                   appPackageUrl, appPackageSha256);
    }
}

internal sealed class GitHubUpdateSource : IUpdateSource
{
    private readonly UpdateSourceConfiguration _configuration;

    public GitHubUpdateSource(UpdateSourceConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<AvailableUpdate?> GetAvailableUpdateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Repository))
        {
            throw new InvalidOperationException("The GitHub repository is not configured.");
        }

        var repository       = _configuration.Repository.Trim();
        var releasesEndpoint = "https://api.github.com/repos/" + repository + "/releases";
        var json             = await UpdateSourceHttp.GetStringAsync(releasesEndpoint, ct).ConfigureAwait(false);
        var serializer       = new JavaScriptSerializer();
        if (serializer.DeserializeObject(json) is not object[] releases)
        {
            throw new InvalidOperationException("The GitHub API did not return a valid releases payload.");
        }

        foreach (var releaseObject in releases.OfType<Dictionary<string, object>>())
        {
            var isDraft      = UpdateSourceParsing.ReadBoolean(releaseObject, "draft");
            var isPrerelease = UpdateSourceParsing.ReadBoolean(releaseObject, "prerelease");
            if (isDraft || isPrerelease && !_configuration.IncludePrerelease)
            {
                continue;
            }

            if (!(releaseObject.TryGetValue("assets", out var assetsObject) && assetsObject is object[] assets))
            {
                continue;
            }

            var assetList     = assets.OfType<Dictionary<string, object>>().ToList();
            var manifestAsset = assetList.FirstOrDefault(a => string.Equals(UpdateSourceParsing.ReadString(a, "name"), "release-manifest.json", StringComparison.OrdinalIgnoreCase));

            if (manifestAsset == null)
            {
                continue;
            }

            var manifestUrl = UpdateSourceParsing.ReadString(manifestAsset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                continue;
            }

            var manifestJson = await UpdateSourceHttp.GetStringAsync(manifestUrl!, ct).ConfigureAwait(false);
            var manifest     = serializer.Deserialize<ReleaseManifest>(manifestJson);

            if (manifest == null || !ApplicationVersion.TryParse(manifest.Version, out var version))
            {
                continue;
            }

            var downloadFileName = manifest.Download.FileName;
            if (string.IsNullOrWhiteSpace(downloadFileName))
            {
                continue;
            }

            var targetAsset = assetList.FirstOrDefault(a => string.Equals(UpdateSourceParsing.ReadString(a, "name"), downloadFileName, StringComparison.OrdinalIgnoreCase));
            if (targetAsset == null)
            {
                continue;
            }

            var downloadUrl = UpdateSourceParsing.ReadString(targetAsset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var resolvedVersion = version!;
            var isUpdaterUpdate = manifest.UpdaterUpdateRequired || string.Equals(manifest.Download.Kind, "installer", StringComparison.OrdinalIgnoreCase);

            // Resolve the application package URL from the release assets as a fallback for after an updater-installer
            // update has been applied.
            string? appPackageUrl    = null;
            string? appPackageSha256 = null;
            if (isUpdaterUpdate && !string.IsNullOrWhiteSpace(manifest.ApplicationPackage.FileName))
            {
                var appPackageAsset = assetList.FirstOrDefault(a => string.Equals(UpdateSourceParsing.ReadString(a, "name"), manifest.ApplicationPackage.FileName, StringComparison.OrdinalIgnoreCase));
                if (appPackageAsset != null)
                {
                    appPackageUrl    = UpdateSourceParsing.ReadString(appPackageAsset, "browser_download_url");
                    appPackageSha256 = manifest.ApplicationPackage.Sha256;
                }
            }

            return new AvailableUpdate(resolvedVersion, downloadUrl!, resolvedVersion.NormalizedValue, manifest.Download.Sha256, isUpdaterUpdate,
                                       appPackageUrl, appPackageSha256);
        }

        return null;
    }
}

internal static class UpdateSourceHttp
{
    public static async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using (var response = await UpdaterHttpClient.Shared.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}

internal static class UpdateSourceParsing
{
    public static string? ReadString(IDictionary<string, object> dictionary, string key)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return Convert.ToString(value);
    }

    public static bool ReadBoolean(IDictionary<string, object> dictionary, string key)
    {
        if (!dictionary.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        return Convert.ToBoolean(value);
    }

    public static string? NormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value!.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("sha256:".Length);
        }

        normalized = normalized.Replace("-", string.Empty).Replace(" ", string.Empty);

        return normalized.Length == 0 ? null : normalized;
    }
}
