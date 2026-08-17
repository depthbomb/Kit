using System.Net.Http;
using System.Net.Http.Headers;

namespace Kit.Updater;

internal static class UpdaterHttpClient
{
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);

    private static HttpClient Shared { get; } = Create();

    public static async Task<HttpResponseMessage> GetAsync(string                 url,
                                                           HttpCompletionOption completionOption,
                                                           CancellationToken    ct,
                                                           long?                rangeStart = null,
                                                           string?              ifRange    = null)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                var response = await SendOnceAsync(url, completionOption, rangeStart, ifRange, ct).ConfigureAwait(false);
                if (!IsTransientStatusCode(response.StatusCode) || attempt == MaximumAttempts)
                {
                    return response;
                }

                var delay = ResolveRetryDelay(response, attempt);
                response.Dispose();
                LogRetry(attempt, delay, "HTTP " + (int)response.StatusCode);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < MaximumAttempts
                                               && !ct.IsCancellationRequested
                                               && (exception is HttpRequestException || exception is TimeoutException))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                LogRetry(attempt, delay, exception.GetType().Name);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("The HTTP retry loop completed unexpectedly.");
    }

    private static async Task<HttpResponseMessage> SendOnceAsync(string                 url,
                                                                 HttpCompletionOption completionOption,
                                                                 long?                rangeStart,
                                                                 string?              ifRange,
                                                                 CancellationToken    ct)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        using (var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            if (rangeStart is > 0)
            {
                request.Headers.Range = new RangeHeaderValue(rangeStart, null);
                if (!string.IsNullOrWhiteSpace(ifRange))
                {
                    request.Headers.TryAddWithoutValidation("If-Range", ifRange);
                }
            }

            headerTimeout.CancelAfter(HeaderTimeout);
            try
            {
                return await Shared.SendAsync(request, completionOption, headerTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("The server did not respond within the update connection timeout.");
            }
        }
    }

    private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode is 408 or 429 || numericStatusCode >= 500;
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return LimitRetryDelay(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return LimitRetryDelay(delay);
            }
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
    }

    private static TimeSpan LimitRetryDelay(TimeSpan delay)
        => delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;

    private static void LogRetry(int attempt, TimeSpan delay, string reason)
    {
        DiagnosticLog.Warning("http.retry",
            new KeyValuePair<string, string?>("attempt", attempt.ToString()),
            new KeyValuePair<string, string?>("delayMilliseconds", ((long)delay.TotalMilliseconds).ToString()),
            new KeyValuePair<string, string?>("reason", reason));
    }

    private static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kit.Updater/1.0");
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }
}
