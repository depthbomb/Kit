using System.Net.Http;

namespace Kit.Updater;

internal static class UpdaterHttpClient
{
    public static HttpClient Shared { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kit.Updater/1.0");
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
