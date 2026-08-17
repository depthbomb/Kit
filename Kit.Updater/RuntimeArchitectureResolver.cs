namespace Kit.Updater;

internal static class RuntimeArchitectureResolver
{
    private static readonly HashSet<string> SupportedArchitectures = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "x86",
        "x64",
        "arm64"
    };

    public static bool IsSupported(string? architecture)
        => SupportedArchitectures.Contains(NormalizeConfiguredValue(architecture));

    public static string Resolve(string? architecture)
    {
        var normalized = NormalizeConfiguredValue(architecture);
        if (!string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var nativeArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")
                                 ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
                                 ?? string.Empty;
        if (nativeArchitecture.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "arm64";
        }

        return Environment.Is64BitOperatingSystem ? "x64" : "x86";
    }

    private static string NormalizeConfiguredValue(string? architecture)
        => string.IsNullOrWhiteSpace(architecture) ? "auto" : architecture!.Trim().ToLowerInvariant();
}
