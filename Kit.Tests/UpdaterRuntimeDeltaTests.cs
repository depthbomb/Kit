using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Kit.Tests;

[TestClass]
public sealed class UpdaterRuntimeDeltaTests
{
    [TestMethod]
    public void SelectsDeltaOnlyForMatchingInstalledVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-runtime-delta-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new UpdaterConfiguration
            {
                ApplicationName = "Test App",
                LaunchExecutable = "app.exe",
                UpdateSource = new UpdateSourceConfiguration { Type = "json", Url = "https://example.test/release.json" }
            };
            var runtime = new UpdaterRuntime(configuration, root);
            ApplicationVersion.TryParse("1.0.0", out var currentVersion);
            ApplicationVersion.TryParse("2.0.0", out var targetVersion);
            var current = new LocalApplicationInstallation(currentVersion!, Path.Combine(root, "app-1.0.0"), Path.Combine(root, "app-1.0.0", "app.exe"));
            var update = new AvailableUpdate(
                targetVersion!,
                "https://example.test/full.zip",
                "2.0.0",
                "full",
                deltaPackages: new[]
                {
                    new AvailableDeltaPackage(currentVersion!, "https://example.test/delta.zip", "delta", new[] { "old.dll" })
                });

            var result = runtime.CheckForProvidedUpdate(current, update);

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.IsTrue(result.AvailableUpdate!.IsDelta);
            Assert.AreEqual("https://example.test/delta.zip", result.AvailableUpdate.DownloadUrl);
            Assert.AreEqual(current.DirectoryPath, result.AvailableUpdate.DeltaBaseDirectory);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task InstallsDeltaIntoNewVersionDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-runtime-delta-test-" + Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "app-1.0.0");
        var deltaSource = Path.Combine(root, "delta-source");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(deltaSource);
        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, "app.exe"), "old");
            File.WriteAllText(Path.Combine(baseDirectory, "unchanged.dll"), "same");
            File.WriteAllText(Path.Combine(baseDirectory, "removed.dll"), "remove");
            File.WriteAllText(Path.Combine(deltaSource, "app.exe"), "new");
            File.WriteAllText(Path.Combine(deltaSource, "added.dll"), "added");
            var deltaZip = Path.Combine(root, "delta.zip");
            ZipFile.CreateFromDirectory(deltaSource, deltaZip);

            var configuration = new UpdaterConfiguration
            {
                ApplicationName = "Test App",
                LaunchExecutable = "app.exe",
                Installation = new InstallationConfiguration
                {
                    RequireIntegrityVerification = true,
                    ExtractionLayout = "direct",
                    CompressFiles = false
                },
                UpdateSource = new UpdateSourceConfiguration { Type = "json", Url = "https://example.test/release.json" }
            };
            ApplicationVersion.TryParse("2.0.0", out var targetVersion);
            var finalFiles = new[]
            {
                FileReference("app.exe", "new"),
                FileReference("unchanged.dll", "same"),
                FileReference("added.dll", "added")
            };
            var update = new AvailableUpdate(
                targetVersion!,
                new Uri(deltaZip).AbsoluteUri,
                "2.0.0",
                HashSha256(deltaZip),
                applicationPackageFiles: finalFiles,
                deltaBaseDirectory: baseDirectory,
                deltaDeletedFiles: new[] { "removed.dll" });

            var installed = await new UpdaterRuntime(configuration, root)
                .DownloadAndInstallUpdateAsync(update, null, CancellationToken.None);

            Assert.AreEqual("new", File.ReadAllText(Path.Combine(installed.DirectoryPath, "app.exe")));
            Assert.AreEqual("same", File.ReadAllText(Path.Combine(installed.DirectoryPath, "unchanged.dll")));
            Assert.AreEqual("added", File.ReadAllText(Path.Combine(installed.DirectoryPath, "added.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(installed.DirectoryPath, "removed.dll")));
            Assert.IsTrue(Directory.Exists(baseDirectory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static ReleasePackageFileReference FileReference(string path, string contents)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
        using var algorithm = SHA512.Create();
        return new ReleasePackageFileReference
        {
            Path = path,
            Size = bytes.Length,
            Sha512 = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty)
        };
    }

    private static string HashSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
    }
}
