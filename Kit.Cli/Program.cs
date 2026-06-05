using Kit.Cli.Commands;

namespace Kit.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var parseResult = CommandLine.Parse(args);
            if (parseResult.ShowUsage)
            {
                Console.WriteLine(CommandLine.BuildUsage());
                return parseResult.ExitCode;
            }

            var kitRc   = KitRcLoader.TryLoadFromCurrentDirectory();
            var command = parseResult.Command!;
            var commandWithConfig = kitRc == null
                ? command
                : new RootCommand(command.Name, command.Options, kitRc);

            return commandWithConfig.Name switch
            {
                CommandName.Stamp    => StampCommand.Run(commandWithConfig),
                CommandName.Inspect  => InspectCommand.Run(commandWithConfig),
                CommandName.Manifest => ManifestCommand.Run(commandWithConfig),
                CommandName.Release  => ReleaseCommand.Run(commandWithConfig),
                _                    => throw new InvalidOperationException("Unsupported command.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
