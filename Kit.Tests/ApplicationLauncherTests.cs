using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kit.Tests;

[TestClass]
public sealed class ApplicationLauncherTests
{
    [DataTestMethod]
    [DataRow("plain", "plain")]
    [DataRow("", "\"\"")]
    [DataRow("C:\\Program Files\\App\\file.txt", "\"C:\\Program Files\\App\\file.txt\"")]
    [DataRow("C:\\Program Files\\App\\", "\"C:\\Program Files\\App\\\\\"")]
    [DataRow("say \"hi\"", "\"say \\\"hi\\\"\"")]
    public void QuoteArgumentUsesWindowsEscapingRules(string argument, string expected)
    {
        Assert.AreEqual(expected, ApplicationLauncher.QuoteArgument(argument));
    }
}
