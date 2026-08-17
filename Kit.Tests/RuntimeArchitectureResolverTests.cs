using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class RuntimeArchitectureResolverTests
{
    [DataTestMethod]
    [DataRow("x86")]
    [DataRow("x64")]
    [DataRow("arm64")]
    public void ResolvePreservesExplicitArchitecture(string architecture)
    {
        Assert.AreEqual(architecture, RuntimeArchitectureResolver.Resolve(architecture));
    }

    [TestMethod]
    public void PackageArchitectureMatchIsExact()
    {
        const string package = "Microsoft.WindowsAppRuntime.2_2.4.0.0_x64__8wekyb3d8bbwe";

        Assert.IsTrue(AppRuntimeChecker.PackageMatchesArchitecture(package, "x64"));
        Assert.IsFalse(AppRuntimeChecker.PackageMatchesArchitecture(package, "x86"));
    }
}
