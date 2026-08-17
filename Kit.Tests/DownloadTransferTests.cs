using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Kit.Tests;

[TestClass]
public sealed class DownloadTransferTests
{
    [TestMethod]
    public async Task CopyToFileAsyncAppendsResumedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), "kit-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "abc");
            long reportedBytes = 0;
            using (var source = new MemoryStream(Encoding.UTF8.GetBytes("def")))
            {
                await DownloadTransfer.CopyToFileAsync(
                    source,
                    path,
                    6,
                    CancellationToken.None,
                    (bytes, _) => reportedBytes = bytes,
                    append: true,
                    initialBytes: 3);
            }

            Assert.AreEqual("abcdef", File.ReadAllText(path));
            Assert.AreEqual(6, reportedBytes);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
