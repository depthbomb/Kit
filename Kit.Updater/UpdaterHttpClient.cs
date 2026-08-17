using System.Net.Http;

namespace Kit.Updater;

internal static class UpdaterHttpClient
{
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);

    private static HttpClient Shared { get; } = Create();

    public static async Task<HttpResponseMessage> GetAsync(string                 url,
                                                           HttpCompletionOption completionOption,
                                                           CancellationToken    ct)
    {
        using (var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            headerTimeout.CancelAfter(HeaderTimeout);
            try
            {
                return await Shared.GetAsync(url, completionOption, headerTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("The server did not respond within the update connection timeout.");
            }
        }
    }

    private static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kit.Updater/1.0");
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }
}
