using Shared;

namespace Kit.Updater;

internal static class UpdaterConfigurationValidator
{
    public static void Validate(UpdaterConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ApplicationName))
        {
            throw new InvalidOperationException("The stamped configuration is missing ApplicationName.");
        }

        if (string.IsNullOrWhiteSpace(configuration.InitialVersion))
        {
            throw new InvalidOperationException("The stamped configuration is missing InitialVersion.");
        }

        if (string.IsNullOrWhiteSpace(configuration.LaunchExecutable))
        {
            throw new InvalidOperationException("The stamped configuration is missing LaunchExecutable.");
        }

        if (configuration.UpdateSource == null)
        {
            throw new InvalidOperationException("The stamped configuration is missing UpdateSource.");
        }

        if (configuration.UpdatePolicy == null)
        {
            throw new InvalidOperationException("The stamped configuration is missing UpdatePolicy.");
        }

        var updatePolicyMode = configuration.UpdatePolicy.Mode.Trim();
        if (updatePolicyMode.Length == 0)
        {
            return;
        }

        if (!string.Equals(updatePolicyMode, "optional", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(updatePolicyMode, "required", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(updatePolicyMode, "minimum-version-required", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The stamped configuration contains an unsupported updatePolicy.mode value.");
        }

        if (string.Equals(updatePolicyMode, "minimum-version-required", StringComparison.OrdinalIgnoreCase)
            && !ApplicationVersion.TryParse(configuration.UpdatePolicy.MinimumVersion, out _))
        {
            throw new InvalidOperationException("The stamped configuration is missing a valid updatePolicy.minimumVersion value.");
        }
    }
}
