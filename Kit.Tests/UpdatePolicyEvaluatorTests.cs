using Kit.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;

namespace Kit.Tests;

[TestClass]
public sealed class UpdatePolicyEvaluatorTests
{
    [TestMethod]
    public void RequiredPolicyCannotBeBypassedBySkippedOutcome()
    {
        var current = Installation("1.0.0");
        var update = Available("2.0.0");
        var result = new UpdateCheckResult(false, current, update, wasSkipped: true);

        var plan = UpdatePolicyEvaluator.CreatePlan(new UpdatePolicyConfiguration { Mode = "required" }, current, result);

        Assert.AreEqual(UpdatePlanKind.InstallApplicationUpdate, plan.Kind);
    }

    [TestMethod]
    public void OptionalSkippedOutcomeLaunchesCurrentVersion()
    {
        var current = Installation("1.0.0");
        var update = Available("2.0.0");
        var result = new UpdateCheckResult(false, current, update, wasSkipped: true);

        var plan = UpdatePolicyEvaluator.CreatePlan(new UpdatePolicyConfiguration { Mode = "optional" }, current, result);

        Assert.AreEqual(UpdatePlanKind.LaunchCurrent, plan.Kind);
        Assert.IsTrue(plan.WasSkipped);
    }

    [TestMethod]
    public void MinimumPolicyInstallsWhenCurrentVersionIsTooOld()
    {
        var current = Installation("1.0.0");
        var update = Available("2.0.0");
        var result = new UpdateCheckResult(true, current, update);

        var plan = UpdatePolicyEvaluator.CreatePlan(
            new UpdatePolicyConfiguration { Mode = "minimum-version-required", MinimumVersion = "1.5.0" },
            current,
            result);

        Assert.AreEqual(UpdatePlanKind.InstallApplicationUpdate, plan.Kind);
    }

    private static LocalApplicationInstallation Installation(string version)
    {
        ApplicationVersion.TryParse(version, out var parsed);
        return new LocalApplicationInstallation(parsed!, "C:\\app-" + version, "C:\\app-" + version + "\\app.exe");
    }

    private static AvailableUpdate Available(string version)
    {
        ApplicationVersion.TryParse(version, out var parsed);
        return new AvailableUpdate(parsed!, "https://example.test/app.zip", version, "00");
    }
}
