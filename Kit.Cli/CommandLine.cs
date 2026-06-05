using System.Text;

namespace Kit.Cli;

internal enum CommandName
{
    Stamp,
    Inspect,
    Manifest,
    Release
}

internal sealed class RootCommand
{
    public RootCommand(CommandName name, IReadOnlyDictionary<string, string> options, KitRcContext? kitRc = null)
    {
        Name    = name;
        Options = options;
        KitRc   = kitRc;
    }

    public CommandName Name { get; }

    public IReadOnlyDictionary<string, string> Options { get; }

    public KitRcContext? KitRc { get; }
}

internal sealed class CommandLineParseResult
{
    public RootCommand? Command { get; init; }

    public bool ShowUsage { get; init; }

    public int ExitCode { get; init; }
}

internal static class CommandLine
{
    public static CommandLineParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandLineParseResult { ShowUsage = true, ExitCode = 1 };
        }

        var commandText = args[0].Trim();
        if (IsHelp(commandText))
        {
            return new CommandLineParseResult { ShowUsage = true, ExitCode = 0 };
        }

        var commandName = commandText.ToLowerInvariant() switch
        {
            "stamp"    => CommandName.Stamp,
            "inspect"  => CommandName.Inspect,
            "manifest" => CommandName.Manifest,
            "release"  => CommandName.Release,
            _          => throw new InvalidOperationException("Unknown command: " + args[0])
        };

        var options = ParseOptions(args.Skip(1));
        if (options.ContainsKey("help"))
        {
            return new CommandLineParseResult { ShowUsage = true, ExitCode = 0 };
        }

        return new CommandLineParseResult
        {
            Command = new RootCommand(commandName, options)
        };
    }

    public static string BuildUsage()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Usage:");
        builder.AppendLine("\tkit stamp --input <blank-bootstrapper.exe> --config <stamp-config.json> [--output <stamped-bootstrapper.exe>]");
        builder.AppendLine("\tkit inspect --input <stamped-bootstrapper.exe>");
        builder.AppendLine("\tkit manifest  --version <release version> --updater <stamped-updater.exe> --package <app-package.zip> [--installer <updater-refresh-installer.exe>] [--output <release-manifest.json>] [--updater-update-required <true|false>]");
        builder.AppendLine("\tkit release --app-dir <app-dir-path> --config <stamp-config.json> --updater <blank-updater.exe> [--version <release-version>] [--output-dir <output-dir-path>] [--package-name <app-package.zip>] [--updater-update-required <true|false>] [--installer-command <command>] [--installer-args <args>] [--installer-path <installer-path>]");
        builder.AppendLine();
        builder.AppendLine("Configuration:");
        builder.AppendLine("\tkit reads .kitrc, .kitrc.yml, or .kitrc.yaml from the current directory or a parent directory.");
        builder.AppendLine("\tValues in .kitrc act as defaults and are overridden by command-line options.");
        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine("\t--help, -h    Show usage");
        return builder.ToString();
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var optionList = args.ToList();
        var options    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < optionList.Count; index++)
        {
            var argument = optionList[index];
            if (IsHelp(argument))
            {
                options["help"] = "true";
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected argument: " + argument);
            }

            string key;
            string value;

            var separatorIndex = argument.IndexOf('=');
            if (separatorIndex >= 0)
            {
                key   = argument[2..separatorIndex];
                value = argument[(separatorIndex + 1)..];
            }
            else
            {
                key = argument[2..];
                if (index + 1 >= optionList.Count)
                {
                    throw new InvalidOperationException("Missing value for option --" + key);
                }

                value = optionList[++index];
            }

            if (key.Length == 0)
            {
                throw new InvalidOperationException("Encountered an empty option name.");
            }

            if (!options.TryAdd(key, value))
            {
                throw new InvalidOperationException("Duplicate option --" + key);
            }
        }

        return options;
    }

    private static bool IsHelp(string value) => string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
}
