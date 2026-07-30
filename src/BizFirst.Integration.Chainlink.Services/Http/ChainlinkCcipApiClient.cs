namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Low-level CCIP API v2 REST client (api.ccip.chain.link/v2) — HTTP only, no ABI/on-chain logic
/// (see <see cref="ChainlinkMessageBuilder"/>/<see cref="ChainlinkOnChainReader"/> for that).
/// Registered as a Singleton (see ChainlinkDependency) — safe because, like BinanceApiClient, this
/// class never resolves a credential itself; the optional CCIP API key arrives as a plain per-call
/// parameter.
///
/// Responses are hand-parsed via JsonDocument/JsonElement, not deserialized into POCOs — matching
/// the established convention across this codebase (BinanceApiClient's own doc comment: "no project
/// in AI/ExecutionNodes defines a custom JsonConverter"). This also sidesteps a real, confirmed risk
/// found during design review: the exact real field-name casing of the CCIP API's response was never
/// settled with full confidence (00_INDEX.md §17 open question 13 — a re-fetch of the same live docs
/// page returned a contradictory result on the endpoint path spelling). Hand-parsing with
/// <c>TryGetProperty</c> against a short list of candidate field names per value degrades gracefully
/// (a field that isn't found comes back null, not a thrown/swallowed deserialization exception) —
/// safer than a POCO with <c>[JsonPropertyName]</c> attributes asserting a specific casing with more
/// confidence than this pass could actually establish.
///
/// Correction applied during implementation, not assumed from the design doc: the message-retrieval
/// path used below is <c>messages/{messageId}</c> (plural) per the design review's best-available
/// evidence, but per §17 open question 13 this still needs a human with direct browser/Postman access
/// to reconfirm before this client is pointed at production traffic.
/// </summary>
public sealed class ChainlinkCcipApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChainlinkCcipApiClient> _logger;

    public ChainlinkCcipApiClient(HttpClient httpClient, ILogger<ChainlinkCcipApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>CCIP-MSG02 — GET messages/{messageId}. Most CCIP API endpoints are publicly accessible; apiKey is optional.</summary>
    public async Task<JsonElement> GetMessageAsync(string messageID, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        // No leading "/" on the relative path — a leading slash here would silently discard the
        // HttpClient.BaseAddress's own "/v2/" segment (a real pitfall flagged during design review,
        // 00_INDEX.md §12.2).
        return await SendAsync(HttpMethod.Get, $"messages/{Uri.EscapeDataString(messageID)}", credential, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CCIP-LNE01 — GET chains. Optionally filtered to lanes originating from a specific source network.</summary>
    public async Task<JsonElement> GetChainsAsync(string? sourceNetworkName, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(sourceNetworkName)
            ? "chains"
            : $"chains?sourceNetworkName={Uri.EscapeDataString(sourceNetworkName)}";
        return await SendAsync(HttpMethod.Get, path, credential, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CCIP-LNE02 — GET lanes. Exact query parameters unconfirmed (00_INDEX.md §17 open question 1) — best-effort against a source/destination chain-selector pair.</summary>
    public async Task<JsonElement> GetLaneLatencyAsync(string sourceChainSelector, string destinationChainSelector, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        var path = $"lanes?sourceChainSelector={Uri.EscapeDataString(sourceChainSelector)}&destinationChainSelector={Uri.EscapeDataString(destinationChainSelector)}";
        return await SendAsync(HttpMethod.Get, path, credential, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CCIP-LNE03 — GET verifiers. "Verifiers" is CCIP's own term for the Risk Management Network's blessing role (00_INDEX.md §4.1).</summary>
    public async Task<JsonElement> GetVerifiersAsync(string? chainSelector, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(chainSelector)
            ? "verifiers"
            : $"verifiers?chainSelector={Uri.EscapeDataString(chainSelector)}";
        return await SendAsync(HttpMethod.Get, path, credential, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string relativePath, ChainlinkApiCredential? credential, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        if (credential is not null)
            request.Headers.Add("x-api-key", credential.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChainlinkApiException("CCIP API request timed out.", httpStatusCode: 504);
        }
        catch (HttpRequestException ex)
        {
            throw new ChainlinkApiException($"Network error calling the CCIP API: {ex.Message}", httpStatusCode: 502);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw BuildApiException(response, body);

            if (string.IsNullOrWhiteSpace(body))
                throw new ChainlinkApiException("CCIP API returned an empty response body.", (int)response.StatusCode);

            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                // A 2xx response with a non-JSON body (e.g. an HTML error page from an intermediary
                // proxy/CDN) is a real, plausible failure mode, not merely a theoretical one — wrap it
                // into the same ChainlinkApiException path every other failure goes through instead of
                // letting a raw JsonException escape this client with an inconsistent error shape.
                throw new ChainlinkApiException($"CCIP API returned a non-JSON response body: {ex.Message}", (int)response.StatusCode);
            }
        }
    }

    private static ChainlinkApiException BuildApiException(HttpResponseMessage response, string body)
    {
        var message = $"CCIP API call failed with HTTP {(int)response.StatusCode}.";

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && TryGetAny(document.RootElement, out var messageElement, "message", "error", "msg")
                    && messageElement.ValueKind == JsonValueKind.String)
                {
                    message = messageElement.GetString() ?? message;
                }
            }
            catch (JsonException)
            {
                // Body wasn't the expected {"message": "..."}-shaped error object — fall back to the generic HTTP-status message.
            }
        }

        return new ChainlinkApiException(message, (int)response.StatusCode);
    }

    /// <summary>
    /// Tries each candidate property name in order, returning the first match. The real CCIP API's
    /// exact field-name casing was never settled with full confidence during design review (§17 open
    /// question 13) — this degrades gracefully across the most plausible candidates instead of
    /// asserting one specific name.
    /// </summary>
    internal static bool TryGetAny(JsonElement element, out JsonElement value, params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            if (element.TryGetProperty(name, out value)) return true;
        }
        value = default;
        return false;
    }
}
