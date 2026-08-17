namespace Kit.Updater;

internal static class UpdateChannel
{
    public static string Normalize(string? channel)
        => string.IsNullOrWhiteSpace(channel) ? "stable" : channel!.Trim().ToLowerInvariant();

    public static bool IsValid(string? channel)
        => Normalize(channel).All(character => character is >= 'a' and <= 'z'
                                                 or >= '0' and <= '9'
                                                 or '-'
                                                 or '_'
                                                 or '.');

    public static bool Matches(string? expected, string? actual)
        => string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);
}
