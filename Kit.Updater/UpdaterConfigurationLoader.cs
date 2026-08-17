using Shared;
using System.Web.Script.Serialization;

namespace Kit.Updater;

internal static class UpdaterConfigurationLoader
{
    public static UpdaterConfiguration Load(string executablePath)
    {
        var configurationJson = StampPayload.ReadConfigurationJson(executablePath);
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var configuration = serializer.Deserialize<UpdaterConfiguration>(configurationJson)
                            ?? throw new InvalidOperationException("The updater configuration is invalid.");
        UpdaterConfigurationValidator.Validate(configuration);
        return configuration;
    }
}
