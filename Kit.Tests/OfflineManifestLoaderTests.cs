using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Web.Script.Serialization;

namespace Kit.Tests;

[TestClass]
public sealed class OfflineManifestLoaderTests
{
    [TestMethod]
    public void LoadsPackageRelativeToManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kit-offline-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var packagePath = Path.Combine(directory, "application.zip");
            File.WriteAllBytes(packagePath, new byte[] { 1, 2, 3 });
            File.WriteAllText(Path.Combine(directory, "release.json"), new JavaScriptSerializer().Serialize(new
            {
                ApplicationName = "Test App",
                Version = "2.0.0",
                Channel = "stable",
                Download = new { Kind = "application", FileName = "application.zip", Sha256 = "abc" }
            }));

            var update = OfflineManifestLoader.Load(Path.Combine(directory, "release.json"), "Test App", "stable");

            Assert.AreEqual(new Uri(packagePath).AbsoluteUri, update.DownloadUrl);
            Assert.AreEqual("2.0.0", update.Version.NormalizedValue);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void RejectsPackageOutsideManifestDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kit-offline-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "release.json"), new JavaScriptSerializer().Serialize(new
            {
                ApplicationName = "Test App",
                Version = "2.0.0",
                Channel = "stable",
                Download = new { Kind = "application", FileName = "..\\outside.zip", Sha256 = "abc" }
            }));

            Assert.ThrowsException<InvalidOperationException>(() =>
                OfflineManifestLoader.Load(Path.Combine(directory, "release.json"), "Test App", "stable"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
