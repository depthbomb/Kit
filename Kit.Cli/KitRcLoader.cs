using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kit.Cli;

internal static class KitRcLoader
{
    private static readonly string[] CandidateNames =
    [
        ".kitrc",
        ".kitrc.yml",
        ".kitrc.yaml"
    ];

    public static KitRcContext? TryLoadFromCurrentDirectory()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null)
        {
            foreach (var candidateName in CandidateNames)
            {
                var candidatePath = Path.Combine(directory.FullName, candidateName);
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                return Load(candidatePath);
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static KitRcContext Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The kitrc file was not found.", fullPath);
        }

        var deserializer = new DeserializerBuilder()
                           .WithNamingConvention(CamelCaseNamingConvention.Instance)
                           .IgnoreUnmatchedProperties()
                           .Build();

        var yaml = File.ReadAllText(fullPath);
        var configuration = deserializer.Deserialize<KitRcConfiguration>(yaml)
                            ?? throw new InvalidOperationException("The kitrc file is empty or invalid.");

        return new KitRcContext(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory, configuration);
    }
}

internal readonly record struct ResolvedOptionValue(string Value, string? BaseDirectory)
{
    public string ResolvePath()
        => KitRcPathResolver.ResolvePath(Value, BaseDirectory);
}

internal static class KitRcPathResolver
{
    public static string ResolvePath(string path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A path value cannot be empty.");
        }

        var trimmed = path.Trim();
        if (baseDirectory == null || Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, trimmed));
    }
}

internal static class KitRcOptionResolver
{
    public static ResolvedOptionValue GetRequiredPath(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector)
        => GetPath(command, optionName, selector, required: true);

    public static ResolvedOptionValue GetOptionalPath(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector, ResolvedOptionValue defaultValue)
    {
        if (TryGetOption(command, optionName, selector, out var value, out var baseDirectory))
        {
            return new ResolvedOptionValue(value, baseDirectory);
        }

        return defaultValue;
    }

    public static ResolvedOptionValue GetRequiredValue(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector)
        => GetValue(command, optionName, selector, required: true);

    public static ResolvedOptionValue GetOptionalValue(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector, string defaultValue)
    {
        if (TryGetOption(command, optionName, selector, out var value, out var baseDirectory))
        {
            return new ResolvedOptionValue(value, baseDirectory);
        }

        return new ResolvedOptionValue(defaultValue, null);
    }

    public static bool GetOptionalBoolean(RootCommand command, string optionName, Func<KitRcCommandOptions, bool?> selector, bool defaultValue = false)
    {
        if (command.Options.TryGetValue(optionName, out var cliValue) && !string.IsNullOrWhiteSpace(cliValue))
        {
            return ParseBoolean(cliValue, optionName);
        }

        var section = GetSection(command);
        if (section != null)
        {
            var configuredValue = selector(section);
            if (configuredValue.HasValue)
            {
                return configuredValue.Value;
            }
        }

        return defaultValue;
    }

    private static ResolvedOptionValue GetPath(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector, bool required)
    {
        if (TryGetOption(command, optionName, selector, out var value, out var baseDirectory))
        {
            return new ResolvedOptionValue(value, baseDirectory);
        }

        if (required)
        {
            throw new InvalidOperationException("Missing required option --" + optionName);
        }

        return default;
    }

    private static ResolvedOptionValue GetValue(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector, bool required)
    {
        if (TryGetOption(command, optionName, selector, out var value, out var baseDirectory))
        {
            return new ResolvedOptionValue(value, baseDirectory);
        }

        if (required)
        {
            throw new InvalidOperationException("Missing required option --" + optionName);
        }

        return default;
    }

    private static bool TryGetOption(RootCommand command, string optionName, Func<KitRcCommandOptions, string?> selector, out string value, out string? baseDirectory)
    {
        if (command.Options.TryGetValue(optionName, out var cliValue) && !string.IsNullOrWhiteSpace(cliValue))
        {
            value = cliValue.Trim();
            baseDirectory = null;
            return true;
        }

        var section = GetSection(command);
        if (section != null)
        {
            var configuredValue = selector(section);
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                value = configuredValue.Trim();
                baseDirectory = command.KitRc?.BaseDirectory;
                return true;
            }
        }

        value = string.Empty;
        baseDirectory = null;
        return false;
    }

    private static KitRcCommandOptions? GetSection(RootCommand command)
    {
        var configuration = command.KitRc?.Configuration;
        if (configuration == null)
        {
            return null;
        }

        return command.Name switch
        {
            CommandName.Stamp    => configuration.Stamp,
            CommandName.Inspect  => configuration.Inspect,
            CommandName.Manifest => configuration.Manifest,
            CommandName.Release  => configuration.Release,
            _                    => null
        };
    }

    private static bool ParseBoolean(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Option --" + optionName + " must be either true or false.");
    }
}
