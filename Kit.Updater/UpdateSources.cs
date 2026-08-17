using Shared;
using System.Net.Http;
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
            "json"   => new JsonUpdateSource(configuration.ApplicationName, configuration.UpdateSource),
            "github" => new GitHubUpdateSource(configuration.ApplicationName, configuration.UpdateSource),
            _        => throw new InvalidOperationException("Unsupported update source type: " + configuration.UpdateSource.Type)
        };
    }
}

internal sealed class JsonUpdateSource : IUpdateSource
{
    private readonly string                    _applicationName;
    private readonly UpdateSourceConfiguration _configuration;

    public JsonUpdateSource(string applicationName, UpdateSourceConfiguration configuration)
    {
        _applicationName = applicationName;
        _configuration = configuration;
    }

    public async Task<AvailableUpdate?> GetAvailableUpdateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Url))
        {
            throw new InvalidOperationException("The JSON update source URL is not configured.");
        }

        var configuredUrl = _configuration.Url.Replace("{channel}", Uri.EscapeDataString(UpdateChannel.Normalize(_configuration.Channel)));
        var json       = await UpdateSourceHttp.GetStringAsync(configuredUrl, ct).ConfigureAwait(false);
        var serializer = new JavaScriptSerializer();
        var manifest   = serializer.Deserialize<ReleaseManifest>(json);
        if (manifest == null)
        {
            throw new InvalidOperationException("The JSON update source returned an invalid or empty manifest.");
        }

        var baseUri = new Uri(configuredUrl);
        return ReleaseManifestResolver.ResolveAvailableUpdate(manifest, _applicationName, _configuration.Channel, fileName =>
        {
            if (Uri.IsWellFormedUriString(fileName, UriKind.Absolute))
            {
                return fileName;
            }

            return new Uri(baseUri, fileName).ToString();
        });
    }
}

internal sealed class GitHubUpdateSource : IUpdateSource
{
    private readonly string                    _applicationName;
    private readonly UpdateSourceConfiguration _configuration;

    public GitHubUpdateSource(string applicationName, UpdateSourceConfiguration configuration)
    {
        _applicationName = applicationName;
        _configuration = configuration;
    }

    public async Task<AvailableUpdate?> GetAvailableUpdateAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Repository))
        {
            throw new InvalidOperationException("The GitHub repository is not configured.");
        }

        var repository = _configuration.Repository.Trim();
        var serializer = new JavaScriptSerializer();

        if (!ShouldIncludePrereleases())
        {
            try
            {
                var latestJson = await UpdateSourceHttp.GetStringAsync(BuildLatestReleaseEndpoint(repository), ct).ConfigureAwait(false);
                if (serializer.DeserializeObject(latestJson) is not Dictionary<string, object> latestRelease)
                {
                    throw new InvalidOperationException("The GitHub API did not return a valid release payload.");
                }

                var latestUpdate = await TryResolveReleaseAsync(latestRelease, serializer, ct).ConfigureAwait(false);
                if (latestUpdate != null)
                {
                    return latestUpdate;
                }
            }
            catch (HttpRequestException)
            {
                // Fall back to the paged release list when the latest endpoint is unavailable.
            }
        }

        for (var page = 1;; page++)
        {
            var releasesEndpoint = BuildReleasesEndpoint(repository, page);
            var json             = await UpdateSourceHttp.GetStringAsync(releasesEndpoint, ct).ConfigureAwait(false);

            if (serializer.DeserializeObject(json) is not object[] releases)
            {
                throw new InvalidOperationException("The GitHub API did not return a valid releases payload.");
            }

            if (releases.Length == 0)
            {
                return null;
            }

            foreach (var releaseObject in releases.OfType<Dictionary<string, object>>())
            {
                if (IsSkippedRelease(releaseObject, ShouldIncludePrereleases()))
                {
                    continue;
                }

                var update = await TryResolveReleaseAsync(releaseObject, serializer, ct).ConfigureAwait(false);

                if (update != null)
                {
                    return update;
                }
            }
        }
    }

    private static bool IsSkippedRelease(Dictionary<string, object> releaseObject, bool includePrerelease)
    {
        var isDraft      = UpdateSourceParsing.ReadBoolean(releaseObject, "draft");
        var isPrerelease = UpdateSourceParsing.ReadBoolean(releaseObject, "prerelease");

        return isDraft || isPrerelease && !includePrerelease;
    }

    private async Task<AvailableUpdate?> TryResolveReleaseAsync(Dictionary<string, object> releaseObject, JavaScriptSerializer serializer, CancellationToken ct)
    {
        var isPrerelease = UpdateSourceParsing.ReadBoolean(releaseObject, "prerelease");
        if (isPrerelease && !ShouldIncludePrereleases())
        {
            return null;
        }

        if (!(releaseObject.TryGetValue("assets", out var assetsObject) && assetsObject is object[] assets))
        {
            return null;
        }

        var assetList     = assets.OfType<Dictionary<string, object>>().ToList();
        var manifestAsset = assetList.FirstOrDefault(a => string.Equals(UpdateSourceParsing.ReadString(a, "name"), "release-manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestAsset == null)
        {
            return null;
        }

        var manifestUrl = UpdateSourceParsing.ReadString(manifestAsset, "browser_download_url");
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            return null;
        }

        var manifestJson = await UpdateSourceHttp.GetStringAsync(manifestUrl!, ct).ConfigureAwait(false);
        var manifest     = serializer.Deserialize<ReleaseManifest>(manifestJson);
        if (manifest == null)
        {
            return null;
        }

        try
        {
            return ReleaseManifestResolver.ResolveAvailableUpdate(manifest, _applicationName, _configuration.Channel, fileName =>
            {
                var targetAsset = assetList.FirstOrDefault(a => string.Equals(UpdateSourceParsing.ReadString(a, "name"), fileName, StringComparison.OrdinalIgnoreCase));
                return targetAsset == null ? null : UpdateSourceParsing.ReadString(targetAsset, "browser_download_url");
            });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string BuildLatestReleaseEndpoint(string repository)
        => $"https://api.github.com/repos/{repository}/releases/latest";

    private static string BuildReleasesEndpoint(string repository, int page)
        => $"https://api.github.com/repos/{repository}/releases?per_page=100&page={page}";

    private bool ShouldIncludePrereleases()
        => _configuration.IncludePrerelease || !UpdateChannel.Matches("stable", _configuration.Channel);
}

internal static class UpdateSourceHttp
{
    public static async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using (var response = await UpdaterHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
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
