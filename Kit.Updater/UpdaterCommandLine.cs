namespace Kit.Updater;

internal enum UpdaterCommandMode
{
    UserInterface,
    Check,
    Update
}

internal sealed class UpdaterCommandLineOptions
{
    public UpdaterCommandMode Mode { get; private set; }
    public bool NoLaunch { get; private set; }
    public bool Silent { get; private set; }
    public bool Repair { get; private set; }
    public string? Channel { get; private set; }
    public string? OfflineManifestPath { get; private set; }

    public static bool TryParse(IEnumerable<string> arguments, out UpdaterCommandLineOptions options, out string? error)
    {
        options = new UpdaterCommandLineOptions();
        error = null;

        var argumentList = arguments.ToList();
        for (var index = 0; index < argumentList.Count; index++)
        {
            var rawArgument = argumentList[index];
            var argument = rawArgument.Trim();
            switch (argument.ToLowerInvariant())
            {
                case "--check":
                    if (!SetMode(options, UpdaterCommandMode.Check, out error)) return false;
                    break;
                case "--update":
                    if (!SetMode(options, UpdaterCommandMode.Update, out error)) return false;
                    break;
                case "--silent":
                    options.Silent = true;
                    if (options.Mode == UpdaterCommandMode.UserInterface)
                    {
                        options.Mode = UpdaterCommandMode.Update;
                    }

                    break;
                case "--no-launch":
                    options.NoLaunch = true;
                    break;
                case "--repair":
                    options.Repair = true;
                    if (options.Mode == UpdaterCommandMode.UserInterface)
                    {
                        options.Mode = UpdaterCommandMode.Update;
                    }

                    break;
                case "--channel":
                    if (!TryReadValue(argumentList, ref index, "--channel", out var channel, out error)) return false;
                    if (!UpdateChannel.IsValid(channel))
                    {
                        error = "--channel contains invalid characters.";
                        return false;
                    }

                    options.Channel = UpdateChannel.Normalize(channel);
                    break;
                case "--offline-manifest":
                    if (!TryReadValue(argumentList, ref index, "--offline-manifest", out var manifestPath, out error)) return false;
                    options.OfflineManifestPath = manifestPath;
                    if (options.Mode == UpdaterCommandMode.UserInterface)
                    {
                        options.Mode = UpdaterCommandMode.Update;
                    }

                    break;
                default:
                    error = "Unknown updater option: " + rawArgument;
                    return false;
            }
        }

        if (options.Mode == UpdaterCommandMode.Check && options.NoLaunch)
        {
            error = "--no-launch can only be used with --update or --silent.";
            return false;
        }

        if (options.Mode == UpdaterCommandMode.Check && options.Repair)
        {
            error = "--repair cannot be combined with --check.";
            return false;
        }

        return true;
    }

    private static bool TryReadValue(IReadOnlyList<string> arguments,
                                     ref int                   index,
                                     string                    option,
                                     out string                value,
                                     out string?               error)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            error = "Missing value for " + option + ".";
            return false;
        }

        value = arguments[++index].Trim();
        error = null;
        return true;
    }

    private static bool SetMode(UpdaterCommandLineOptions options, UpdaterCommandMode mode, out string? error)
    {
        if (options.Mode != UpdaterCommandMode.UserInterface && options.Mode != mode)
        {
            error = "Only one of --check or --update may be specified.";
            return false;
        }

        options.Mode = mode;
        error = null;
        return true;
    }
}

internal static class UpdaterExitCode
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int InvalidArguments = 2;
    public const int UpdateAvailable = 10;
    public const int UpdateInstalled = 11;
    public const int SelfUpdateStarted = 12;
    public const int PrerequisiteMissing = 20;
    public const int ApplicationRunning = 21;
}
