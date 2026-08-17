using Shared;

namespace Kit.Updater;

internal enum UpdatePlanKind
{
    LaunchCurrent,
    LaunchInstalledUpdate,
    PromptForApplicationUpdate,
    InstallApplicationUpdate,
    InstallUpdaterUpdate
}

internal sealed class UpdatePlan
{
    public UpdatePlan(UpdatePlanKind                 kind,
                      LocalApplicationInstallation? installation,
                      AvailableUpdate?              update,
                      bool                          wasSkipped = false)
    {
        Kind         = kind;
        Installation = installation;
        Update       = update;
        WasSkipped   = wasSkipped;
    }

    public UpdatePlanKind Kind { get; }
    public LocalApplicationInstallation? Installation { get; }
    public AvailableUpdate? Update { get; }
    public bool WasSkipped { get; }
}

internal static class UpdatePolicyEvaluator
{
    public static UpdatePlan CreatePlan(UpdatePolicyConfiguration     policy,
                                        LocalApplicationInstallation? currentInstallation,
                                        UpdateCheckResult              updateResult)
    {
        EnsureMinimumVersionCanBeSatisfied(policy, currentInstallation, updateResult.AvailableUpdate);

        if (!updateResult.IsUpdateAvailable || updateResult.AvailableUpdate == null)
        {
            if (currentInstallation == null)
            {
                throw new InvalidOperationException("No local installation was found and no update is available to perform a fresh install.");
            }

            if (updateResult.AvailableUpdate != null
                && IsUpdateRequired(policy, currentInstallation)
                && updateResult.AvailableUpdate.Version.CompareTo(currentInstallation.Version) > 0)
            {
                return BuildInstallPlan(updateResult.AvailableUpdate);
            }

            return new UpdatePlan(UpdatePlanKind.LaunchCurrent, currentInstallation, updateResult.AvailableUpdate, updateResult.WasSkipped);
        }

        if (updateResult.AlreadyInstalled)
        {
            return new UpdatePlan(UpdatePlanKind.LaunchInstalledUpdate, updateResult.LaunchInstallation, updateResult.AvailableUpdate);
        }

        if (updateResult.AvailableUpdate.IsUpdaterUpdate)
        {
            return new UpdatePlan(UpdatePlanKind.InstallUpdaterUpdate, currentInstallation, updateResult.AvailableUpdate);
        }

        if (currentInstallation == null || IsUpdateRequired(policy, currentInstallation))
        {
            return new UpdatePlan(UpdatePlanKind.InstallApplicationUpdate, currentInstallation, updateResult.AvailableUpdate);
        }

        return new UpdatePlan(UpdatePlanKind.PromptForApplicationUpdate, currentInstallation, updateResult.AvailableUpdate);
    }

    public static bool IsUpdateRequired(UpdatePolicyConfiguration policy, LocalApplicationInstallation? currentInstallation)
    {
        var mode = policy.Mode.Trim();
        if (mode.Length == 0 || string.Equals(mode, "optional", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(mode, "required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(mode, "minimum-version-required", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (currentInstallation == null || !ApplicationVersion.TryParse(policy.MinimumVersion, out var minimumVersion))
        {
            return true;
        }

        return currentInstallation.Version.CompareTo(minimumVersion) < 0;
    }

    private static UpdatePlan BuildInstallPlan(AvailableUpdate update)
        => new(update.IsUpdaterUpdate ? UpdatePlanKind.InstallUpdaterUpdate : UpdatePlanKind.InstallApplicationUpdate, null, update);

    private static void EnsureMinimumVersionCanBeSatisfied(UpdatePolicyConfiguration     policy,
                                                           LocalApplicationInstallation? currentInstallation,
                                                           AvailableUpdate?              availableUpdate)
    {
        if (!string.Equals(policy.Mode.Trim(), "minimum-version-required", StringComparison.OrdinalIgnoreCase)
            || currentInstallation == null)
        {
            return;
        }

        if (!ApplicationVersion.TryParse(policy.MinimumVersion, out var minimumVersion))
        {
            throw new InvalidOperationException("updatePolicy.minimumVersion must be a valid version when updatePolicy.mode is minimum-version-required.");
        }

        if (currentInstallation.Version.CompareTo(minimumVersion) >= 0)
        {
            return;
        }

        if (availableUpdate == null)
        {
            throw new InvalidOperationException("The installed application version is below the required minimum version and no update is available.");
        }

        if (availableUpdate.Version.CompareTo(minimumVersion) < 0)
        {
            throw new InvalidOperationException("The available update does not satisfy the configured minimum required version.");
        }
    }
}
