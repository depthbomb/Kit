using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class ApplicationVersionTests
{
    [DataTestMethod]
    [DataRow("1.0-../../../outside")]
    [DataRow("1.0-feature/name")]
    [DataRow("1.0-feature\\name")]
    [DataRow("1.0-")]
    [DataRow("1.0+invalid metadata")]
    public void TryParseRejectsUnsafeLabels(string value)
    {
        Assert.IsFalse(ApplicationVersion.TryParse(value, out _));
    }

    [TestMethod]
    public void CompareToUsesSemanticPrereleaseOrdering()
    {
        Assert.IsTrue(ApplicationVersion.TryParse("2.2.0-preview.2", out var preview));
        Assert.IsTrue(ApplicationVersion.TryParse("2.2.0", out var stable));
        Assert.IsTrue(ApplicationVersion.TryParse("2.3.0", out var newer));

        Assert.IsTrue(preview!.CompareTo(stable) < 0);
        Assert.IsTrue(newer!.CompareTo(stable) > 0);
    }
}
