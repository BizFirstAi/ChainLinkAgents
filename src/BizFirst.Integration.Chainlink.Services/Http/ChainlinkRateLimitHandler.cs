namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// DelegatingHandler for the CCIP API's rate-limit signals. Mirrors the real
/// SlackRateLimitHandler/EthereumRateLimitHandler/BinanceRateLimitHandler shape: retry on 429 up to
/// <see cref="MaxRetries"/> times, honor <c>Retry-After</c> when present, fall back to exponential
/// backoff otherwise, and clone request content on retry since <see cref="HttpContent"/> is
/// single-read. Registered Transient (Guideline 10.2) — never Scoped/Singleton, to avoid socket
/// exhaustion under <see cref="System.Net.Http.IHttpClientFactory"/>'s pooled-handler model.
/// </summary>
public sealed class ChainlinkRateLimitHandler : DelegatingHandler
{
    private const int MaxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = null!;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0 && request.Content is not null)
                request.Content = await CloneContentAsync(request.Content, cancellationToken).ConfigureAwait(false);

            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var isRetryable = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                || (int)response.StatusCode is 502 or 503 or 504;

            if (!isRetryable) return response;
            if (attempt == MaxRetries) break;

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static async Task<HttpContent> CloneContentAsync(HttpContent original, CancellationToken cancellationToken)
    {
        var bytes = await original.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var clone = new ByteArrayContent(bytes);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
