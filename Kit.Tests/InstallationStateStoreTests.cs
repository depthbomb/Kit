using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;

namespace Kit.Tests;

[TestClass]
public sealed class InstallationStateStoreTests
{
    [TestMethod]
    public void ResolveCurrentInstallationRollsBackInterruptedActivation()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "app-1.0.0"));
            Directory.CreateDirectory(Path.Combine(root, "app-2.0.0"));
            var configuration = new UpdaterConfiguration
            {
                InitialVersion = "1.0.0",
                LaunchExecutable = "app.exe"
            };

            var state = new InstallationStateStore(configuration, root);
            state.BeginActivation("2.0.0", "1.0.0");

            var resolved = new InstallationStateStore(configuration, root).ResolveCurrentInstallation();

            Assert.AreEqual("1.0.0", resolved!.Version.NormalizedValue);
            Assert.IsFalse(File.Exists(Path.Combine(root, ".kit-pending-activation")));
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
