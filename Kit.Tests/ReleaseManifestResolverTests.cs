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
}
