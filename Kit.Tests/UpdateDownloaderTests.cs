using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class UpdateDownloaderTests
{
    [TestMethod]
    public async Task CopiesOfflinePackageFromFileUri()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kit-download-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.zip");
            var target = Path.Combine(directory, "cache", "target.zip");
            var expected = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(source, expected);

            await new UpdateDownloader().DownloadFileAsync(new Uri(source).AbsoluteUri, target, "1.0.0", null, CancellationToken.None);

            CollectionAssert.AreEqual(expected, File.ReadAllBytes(target));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
