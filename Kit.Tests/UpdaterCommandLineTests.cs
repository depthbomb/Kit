using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class UpdaterCommandLineTests
{
    [TestMethod]
    public void SilentSelectsUpdateMode()
    {
        Assert.IsTrue(UpdaterCommandLineOptions.TryParse(new[] { "--silent", "--no-launch" }, out var options, out _));
        Assert.AreEqual(UpdaterCommandMode.Update, options.Mode);
        Assert.IsTrue(options.Silent);
        Assert.IsTrue(options.NoLaunch);
    }

    [TestMethod]
    public void ConflictingModesAreRejected()
    {
        Assert.IsFalse(UpdaterCommandLineOptions.TryParse(new[] { "--check", "--update" }, out _, out var error));
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void RepairChannelAndOfflineManifestSelectUpdateMode()
    {
        Assert.IsTrue(UpdaterCommandLineOptions.TryParse(
            new[] { "--repair", "--channel", "preview", "--offline-manifest", "release.json", "--no-launch" },
            out var options,
            out _));

        Assert.AreEqual(UpdaterCommandMode.Update, options.Mode);
        Assert.IsTrue(options.Repair);
        Assert.AreEqual("preview", options.Channel);
        Assert.AreEqual("release.json", options.OfflineManifestPath);
    }

    [TestMethod]
    public void RepairCannotBeCombinedWithCheck()
    {
        Assert.IsFalse(UpdaterCommandLineOptions.TryParse(new[] { "--check", "--repair" }, out _, out var error));
        StringAssert.Contains(error, "--repair");
    }
}
