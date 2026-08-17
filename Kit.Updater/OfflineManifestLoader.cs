using Shared;
using System.Web.Script.Serialization;

namespace Kit.Updater;

internal static class OfflineManifestLoader
{
    public static AvailableUpdate Load(string manifestPath, string applicationName, string channel)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("An offline manifest path is required.", nameof(manifestPath));
        }

        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifestPath))
        {
            throw new FileNotFoundException("The offline release manifest was not found.", fullManifestPath);
        }

        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var manifest = serializer.Deserialize<ReleaseManifest>(File.ReadAllText(fullManifestPath))
                       ?? throw new InvalidOperationException("The offline release manifest is invalid.");
        var manifestDirectory = Path.GetDirectoryName(fullManifestPath)
                                ?? throw new InvalidOperationException("The offline manifest directory could not be resolved.");

        return ReleaseManifestResolver.ResolveAvailableUpdate(
            manifest,
            applicationName,
            channel,
            fileName => ResolvePackagePath(manifestDirectory, fileName));
    }

    private static string ResolvePackagePath(string manifestDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
        {
            throw new InvalidOperationException("Offline package filenames must be relative to the manifest directory.");
        }

        var baseDirectory = Path.GetFullPath(manifestDirectory)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var packagePath = Path.GetFullPath(Path.Combine(baseDirectory, fileName));
        var prefix = baseDirectory + Path.DirectorySeparatorChar;
        if (!packagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(packagePath))
        {
            throw new InvalidOperationException("An offline package is missing or resolves outside the manifest directory: " + fileName);
        }

        return new Uri(packagePath).AbsoluteUri;
    }
}
