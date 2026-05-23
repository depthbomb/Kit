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

            return parseResult.Command!.Name switch
            {
                CommandName.Stamp    => StampCommand.Run(parseResult.Command),
                CommandName.Inspect  => InspectCommand.Run(parseResult.Command),
                CommandName.Manifest => ManifestCommand.Run(parseResult.Command),
                CommandName.Release  => ReleaseCommand.Run(parseResult.Command),
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
