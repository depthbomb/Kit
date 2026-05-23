using System.Text.Json;

namespace Kit.Cli.Commands;

internal static class StampCommand
{
    public static int Run(RootCommand command)
    {
        var inputPath  = CommandLine.GetRequiredOption(command.Options, "input");
        var configPath = CommandLine.GetRequiredOption(command.Options, "config");
        var outputPath = command.Options.GetValueOrDefault("output", inputPath);

        var fullInputPath  = Path.GetFullPath(inputPath);
        var fullConfigPath = Path.GetFullPath(configPath);
        var fullOutputPath = Path.GetFullPath(outputPath);

        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("Input updater binary was not found.", fullInputPath);
        }

        if (!File.Exists(fullConfigPath))
        {
            throw new FileNotFoundException("Stamp configuration file was not found.", fullConfigPath);
        }

        var configDirectory    = Path.GetDirectoryName(fullConfigPath) ?? Environment.CurrentDirectory;
        var stampConfiguration = StampConfigurationLoader.Load(fullConfigPath);
        StampPayloadValidator.Validate(stampConfiguration, configDirectory);

        var buildResult = StampPayloadBuilder.Build(stampConfiguration, configDirectory);
        var payloadJson = JsonSerializer.Serialize(buildResult.Payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        StampedUpdaterWriter.Write(fullInputPath, fullOutputPath, payloadJson, buildResult.ResolvedIconPath);

        Console.WriteLine("Stamped updater written to:");
        Console.WriteLine(fullOutputPath);

        return 0;
    }
}
