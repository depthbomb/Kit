using Shared;

namespace Kit.Cli;

internal static class StampPayloadValidator
{
    private static readonly HashSet<string> SupportedUpdateSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "json",
        "github"
    };

    private static readonly HashSet<string> SupportedExtractionLayouts = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "auto",
        "direct",
        "strip-single-root-directory"
    };

    private static readonly HashSet<string> SupportedRuntimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "runtime",
        "desktop-runtime",
        "aspnetcore-runtime",
        "windowsdesktop-runtime"
    };

    private static readonly HashSet<string> SupportedUpdatePolicyModes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "optional",
        "required",
        "minimum-version-required"
    };

    public static void Validate(StampInputConfiguration configuration, string configDirectory)
    {
        RequireValue(configuration.ApplicationName, "applicationName");
        RequireValue(configuration.InitialVersion, "initialVersion");
        RequireValue(configuration.LaunchExecutable, "launchExecutable");

        if (!StampVersion.TryParse(configuration.InitialVersion!))
        {
            throw new InvalidOperationException("initialVersion must be a valid version string.");
        }

        ValidateOptionalFile(configuration.BannerImagePath, configDirectory, null, "bannerImagePath");
        ValidateOptionalFile(configuration.WindowIconPath, configDirectory, ".ico", "windowIconPath");

        var installation = configuration.Installation ?? new InstallationConfiguration();
        if (installation.KeepLastVersions < 0)
        {
            throw new InvalidOperationException("installation.keepLastVersions must be zero or greater.");
        }

        var extractionLayout = installation.ExtractionLayout.Trim();
        if (!SupportedExtractionLayouts.Contains(extractionLayout))
        {
            throw new InvalidOperationException("installation.extractionLayout must be one of: auto, direct, strip-single-root-directory.");
        }

        var updatePolicy     = configuration.UpdatePolicy ?? new UpdatePolicyConfiguration();
        var updatePolicyMode = updatePolicy.Mode.Trim();
        if (!SupportedUpdatePolicyModes.Contains(updatePolicyMode))
        {
            throw new InvalidOperationException("updatePolicy.mode must be one of: optional, required, minimum-version-required.");
        }

        if (string.Equals(updatePolicyMode, "minimum-version-required", StringComparison.OrdinalIgnoreCase)
            && !StampVersion.TryParse(updatePolicy.MinimumVersion))
        {
            throw new InvalidOperationException("updatePolicy.minimumVersion must be a valid version when updatePolicy.mode is minimum-version-required.");
        }

        var updateSource = configuration.UpdateSource ?? throw new InvalidOperationException("updateSource is required.");

        RequireValue(updateSource.Type, "updateSource.type");

        var sourceType = updateSource.Type.Trim();
        if (!SupportedUpdateSources.Contains(sourceType))
        {
            throw new InvalidOperationException("updateSource.type must be either 'json' or 'github'.");
        }

        if (sourceType.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            RequireValue(updateSource.Url, "updateSource.url");
        }

        if (sourceType.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            RequireValue(updateSource.Repository, "updateSource.repository");
        }

        if (!string.IsNullOrWhiteSpace(configuration.RequiredAppRuntimeVersion) && !StampVersion.TryParse(configuration.RequiredAppRuntimeVersion))
        {
            throw new InvalidOperationException("requiredAppRuntimeVersion must be a valid version string.");
        }

        if (configuration.RequiredRuntimes == null)
        {
            return;
        }

        foreach (var runtime in configuration.RequiredRuntimes)
        {
            RequireValue(runtime.Name, "requiredRuntimes[].name");
            RequireValue(runtime.Version, "requiredRuntimes[].version");
            RequireValue(runtime.Type, "requiredRuntimes[].type");

            if (!StampVersion.TryParse(runtime.Version))
            {
                throw new InvalidOperationException($"requiredRuntimes[].version '{runtime.Version}' is not a valid version string.");
            }

            if (!SupportedRuntimeTypes.Contains(runtime.Type))
            {
                throw new InvalidOperationException($"requiredRuntimes[].type '{runtime.Type}' is not supported.");
            }
        }
    }

    private static void ValidateOptionalFile(string? configuredPath, string configDirectory, string? requiredExtension, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return;
        }

        var resolvedPath = configuredPath.Trim();
        if (!Path.IsPathRooted(resolvedPath))
        {
            resolvedPath = Path.Combine(configDirectory, resolvedPath);
        }

        resolvedPath = Path.GetFullPath(resolvedPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(fieldName + " was not found.", resolvedPath);
        }

        if (requiredExtension != null && !string.Equals(Path.GetExtension(resolvedPath), requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(fieldName + " must point to a " + requiredExtension + " file.");
        }
    }

    private static void RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(fieldName + " is required.");
        }
    }
}

internal static class StampVersion
{
    public static bool TryParse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1 && char.IsDigit(trimmed[1]))
        {
            trimmed = trimmed[1..];
        }

        var buildSplit      = trimmed.Split(['+'], 2);
        var prereleaseSplit = buildSplit[0].Split(['-'], 2);
        var numericPart     = prereleaseSplit[0];
        var segments        = numericPart.Split('.');
        if (segments.Length == 0)
        {
            return false;
        }

        if (segments.Any(segment => segment.Length == 0 || !int.TryParse(segment, out _)))
        {
            return false;
        }

        if (prereleaseSplit.Length == 2)
        {
            if (!AreValidLabelSegments(prereleaseSplit[1]))
            {
                return false;
            }
        }

        if (buildSplit.Length == 2 && !AreValidLabelSegments(buildSplit[1]))
        {
            return false;
        }

        return true;
    }

    private static bool AreValidLabelSegments(string value)
        => value.Split('.').All(segment => segment.Length > 0
                                           && segment.All(character => character is >= 'a' and <= 'z'
                                                                       or >= 'A' and <= 'Z'
                                                                       or >= '0' and <= '9'
                                                                       or '-'));
}
