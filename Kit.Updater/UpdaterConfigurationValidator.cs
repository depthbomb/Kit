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

        if (!string.IsNullOrWhiteSpace(configuration.RequiredAppRuntimeVersion) && !Version.TryParse(configuration.RequiredAppRuntimeVersion, out _))
        {
            throw new InvalidOperationException("The stamped configuration contains an invalid requiredAppRuntimeVersion value.");
        }

        if (!UpdateChannel.IsValid(configuration.UpdateSource.Channel))
        {
            throw new InvalidOperationException("The stamped configuration contains an invalid updateSource.channel value.");
        }

        if (!RuntimeArchitectureResolver.IsSupported(configuration.RequiredAppRuntimeArchitecture))
        {
            throw new InvalidOperationException("The stamped configuration contains an invalid requiredAppRuntimeArchitecture value.");
        }

        if (configuration.RequiredRuntimes != null
            && configuration.RequiredRuntimes.Any(runtime => !RuntimeArchitectureResolver.IsSupported(runtime.Architecture)))
        {
            throw new InvalidOperationException("The stamped configuration contains an invalid required runtime architecture.");
        }

        if (configuration.Installation == null || configuration.Installation.LaunchHealthTimeoutSeconds < 0)
        {
            throw new InvalidOperationException("The stamped configuration contains an invalid installation.launchHealthTimeoutSeconds value.");
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
