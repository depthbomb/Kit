using System.Text.Json;

namespace Kit.Cli;

internal static class StampConfigurationLoader
{
    public static StampInputConfiguration Load(string configPath)
    {
        var configJson = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<StampInputConfiguration>(configJson, new JsonSerializerOptions
               {
                   PropertyNamingPolicy = JsonNamingPolicy.CamelCase
               })
               ?? throw new InvalidOperationException("The stamp configuration file is empty or invalid.");
    }
}
