using Shared;
using System.Text.Json;

namespace Kit.Cli.Commands;

internal static class InspectCommand
{
    public static int Run(RootCommand command)
    {
        var inputPath     = CommandLine.GetRequiredOption(command.Options, "input");
        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("Input updater binary was not found.", fullInputPath);
        }

        var payloadJson = StampPayload.ReadConfigurationJson(fullInputPath);
        var payload = JsonSerializer.Deserialize<UpdaterConfiguration>(payloadJson, new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                      })
                      ?? throw new InvalidOperationException("The updater does not contain a valid stamped payload.");

        var formattedJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented        = true
        });

        Console.WriteLine(formattedJson);

        return 0;
    }
}
