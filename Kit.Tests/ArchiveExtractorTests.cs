using System.IO.Compression;
using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class ArchiveExtractorTests
{
    [TestMethod]
    public async Task ExtractAsyncRejectsEntriesOutsideDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-tests-" + Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(root, "package.zip");
        var destination = Path.Combine(root, "destination");
        var outsidePath = Path.Combine(root, "outside.txt");

        try
        {
            Directory.CreateDirectory(destination);
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("../../outside.txt").Open()))
            {
                writer.Write("unsafe");
            }

            var extractor = new ArchiveExtractor(root);
            await Assert.ThrowsExceptionAsync<InvalidDataException>(
                () => extractor.ExtractAsync(archivePath, ".zip", destination, CancellationToken.None));
            Assert.IsFalse(File.Exists(outsidePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
