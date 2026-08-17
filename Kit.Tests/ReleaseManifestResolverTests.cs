using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;

namespace Kit.Tests;

[TestClass]
public sealed class ReleaseManifestResolverTests
{
    [TestMethod]
    public void ResolveAvailableUpdateRejectsAnotherApplication()
    {
        var manifest = new ReleaseManifest
        {
            ApplicationName = "Another App",
            Version = "1.2.3",
            Download = new ReleaseDownloadInstruction
            {
                Kind = "application",
                FileName = "app.zip",
                Sha256 = "00"
            }
        };

        Assert.ThrowsException<InvalidOperationException>(() =>
            ReleaseManifestResolver.ResolveAvailableUpdate(manifest, "Expected App", "stable", _ => "https://example.test/app.zip"));
    }

    [TestMethod]
    public void ResolveAvailableUpdateRejectsAnotherChannel()
    {
        var manifest = new ReleaseManifest
        {
            ApplicationName = "Expected App",
            Version = "1.2.3",
            Channel = "preview",
            Download = new ReleaseDownloadInstruction { Kind = "application", FileName = "app.zip", Sha256 = "00" }
        };

        Assert.ThrowsException<InvalidOperationException>(() =>
            ReleaseManifestResolver.ResolveAvailableUpdate(manifest, "Expected App", "stable", _ => "https://example.test/app.zip"));
    }

    [TestMethod]
    public void ResolvesDeltaPackagesWithFullPackageFallback()
    {
        var manifest = new ReleaseManifest
        {
            ApplicationName = "Expected App",
            Version = "2.0.0",
            Download = new ReleaseDownloadInstruction { Kind = "application", FileName = "app.zip", Sha256 = "full" },
            ApplicationPackage = new ReleasePackageReference
            {
                Files = new List<ReleasePackageFileReference> { new() { Path = "app.exe", Sha512 = "hash", Size = 1 } }
            },
            DeltaPackages = new List<ReleaseDeltaPackageReference>
            {
                new() { FromVersion = "1.0.0", FileName = "delta.zip", Sha256 = "delta", DeletedFiles = new List<string> { "old.dll" } }
            }
        };

        var update = ReleaseManifestResolver.ResolveAvailableUpdate(
            manifest, "Expected App", "stable", file => "https://example.test/" + file);

        Assert.AreEqual("https://example.test/app.zip", update.DownloadUrl);
        Assert.AreEqual(1, update.DeltaPackages.Count);
        Assert.AreEqual("https://example.test/delta.zip", update.DeltaPackages[0].DownloadUrl);
        CollectionAssert.AreEqual(new[] { "old.dll" }, update.DeltaPackages[0].DeletedFiles.ToArray());
    }

    [TestMethod]
    public void IgnoresInvalidDeltaAndKeepsFullPackage()
    {
        var manifest = new ReleaseManifest
        {
            ApplicationName = "Expected App",
            Version = "2.0.0",
            Download = new ReleaseDownloadInstruction { Kind = "application", FileName = "app.zip", Sha256 = "full" },
            DeltaPackages = new List<ReleaseDeltaPackageReference>
            {
                new() { FromVersion = "2.0.0", FileName = "delta.zip", Sha256 = "delta" }
            }
        };

        var update = ReleaseManifestResolver.ResolveAvailableUpdate(
            manifest, "Expected App", "stable", file => "https://example.test/" + file);

        Assert.AreEqual("https://example.test/app.zip", update.DownloadUrl);
        Assert.AreEqual(0, update.DeltaPackages.Count);
    }
}
