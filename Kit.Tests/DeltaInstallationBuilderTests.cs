using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class DeltaInstallationBuilderTests
{
    [TestMethod]
    public void CopiesBaseThenAppliesChangesAndDeletions()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-delta-test-" + Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "base");
        var deltaDirectory = Path.Combine(root, "delta");
        var targetDirectory = Path.Combine(root, "target");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(deltaDirectory);
        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, "unchanged.txt"), "same");
            File.WriteAllText(Path.Combine(baseDirectory, "changed.txt"), "old");
            File.WriteAllText(Path.Combine(baseDirectory, "removed.txt"), "remove");
            File.WriteAllText(Path.Combine(deltaDirectory, "changed.txt"), "new");
            File.WriteAllText(Path.Combine(deltaDirectory, "added.txt"), "added");

            DeltaInstallationBuilder.Build(baseDirectory, deltaDirectory, targetDirectory, new[] { "removed.txt" }, CancellationToken.None);

            Assert.AreEqual("same", File.ReadAllText(Path.Combine(targetDirectory, "unchanged.txt")));
            Assert.AreEqual("new", File.ReadAllText(Path.Combine(targetDirectory, "changed.txt")));
            Assert.AreEqual("added", File.ReadAllText(Path.Combine(targetDirectory, "added.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(targetDirectory, "removed.txt")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void RejectsDeletionOutsideTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-delta-test-" + Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "base");
        var deltaDirectory = Path.Combine(root, "delta");
        var targetDirectory = Path.Combine(root, "target");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(deltaDirectory);
        try
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
                DeltaInstallationBuilder.Build(baseDirectory, deltaDirectory, targetDirectory, new[] { "..\\outside.txt" }, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
