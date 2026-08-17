namespace Kit.Updater;

internal static class DeltaInstallationBuilder
{
    public static void Build(string                  baseDirectory,
                             string                  deltaDirectory,
                             string                  targetDirectory,
                             IEnumerable<string>?    deletedFiles,
                             CancellationToken       ct)
    {
        if (!Directory.Exists(baseDirectory))
        {
            throw new DirectoryNotFoundException("The delta package base installation was not found: " + baseDirectory);
        }

        CopyDirectory(baseDirectory, targetDirectory, overwrite: false, ct);

        foreach (var relativePath in deletedFiles ?? Array.Empty<string>())
        {
            ct.ThrowIfCancellationRequested();
            var targetPath = ResolveContainedPath(targetDirectory, relativePath);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            else if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
        }

        CopyDirectory(deltaDirectory, targetDirectory, overwrite: true, ct);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool overwrite, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(targetDirectory, GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(targetDirectory, GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDirectory);
            File.Copy(file, targetPath, overwrite);
        }
    }

    private static string GetRelativePath(string baseDirectory, string path)
    {
        var root = Path.GetFullPath(baseDirectory)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A delta source path resolves outside its directory.");
        }

        return fullPath.Substring(root.Length);
    }

    private static string ResolveContainedPath(string baseDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("The delta deletion list contains an invalid path.");
        }

        var root = Path.GetFullPath(baseDirectory)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The delta deletion list contains a path outside the installation directory: " + relativePath);
        }

        return resolved;
    }
}
