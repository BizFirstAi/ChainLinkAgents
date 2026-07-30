namespace BizFirst.Integration.Chainlink.Services;

/// <summary>Message resource: build (pure computation), getStatus (HTTP), checkManualExecutionRequired (derived), getTokenTransferRateLimit (on-chain descriptor).</summary>
public sealed class ChainlinkMessageService
{
    private readonly ChainlinkCcipApiClient _apiClient;
    private readonly ChainlinkMessageBuilder _messageBuilder;
    private readonly ILogger<ChainlinkMessageService> _logger;

    public ChainlinkMessageService(ChainlinkCcipApiClient apiClient, ChainlinkMessageBuilder messageBuilder, ILogger<ChainlinkMessageService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _messageBuilder = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>CCIP-MSG01 — message/build. Local, pure computation — no HTTP, no RPC, no exceptions expected from valid input.</summary>
    public ChainlinkMessageBuildResult Build(
        string receiverAddress, string? payloadDataHex, IReadOnlyList<ChainlinkTokenAmount> tokenAmounts,
        string? feeToken, string? extraArgsVersion, long? gasLimit, bool allowOutOfOrderExecution)
    {
        try
        {
            var built = _messageBuilder.Build(receiverAddress, payloadDataHex, tokenAmounts, feeToken, extraArgsVersion, gasLimit, allowOutOfOrderExecution);
            return ChainlinkMessageBuildResult.Ok(built.EncodedMessageJson, built.ExtraArgsBytesHex);
        }
        catch (ArgumentException ex)
        {
            // ChainlinkMessageBuilder throws ArgumentException for several distinct validation
            // failures (bad address shape, bad tokenAmounts.amount, unknown extraArgsVersion, negative
            // gasLimit) — map by ParamName so the error code actually reflects which one occurred,
            // rather than lumping every validation failure under one misleading code.
            var errorCode = ex.ParamName switch
            {
                nameof(receiverAddress) or nameof(feeToken) => "INVALID_ADDRESS",
                "tokenAmounts" => "INVALID_TOKEN_AMOUNT",
                nameof(gasLimit) => "INVALID_GAS_LIMIT",
                _ => "INVALID_EXTRA_ARGS_VERSION",
            };
            return ChainlinkMessageBuildResult.Fail(errorCode, ex.Message);
        }
    }

    /// <summary>CCIP-MSG02 — message/getStatus. HTTP: GET messages/{messageId}.</summary>
    public async Task<ChainlinkMessageGetStatusResult> GetStatusAsync(string messageID, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = await _apiClient.GetMessageAsync(messageID, credential, cancellationToken).ConfigureAwait(false);

            var resolvedMessageID = ReadStringAny(root, "messageId", "id") ?? messageID;
            var executionState = ReadStringAny(root, "state", "executionState", "status");
            var sourceNetworkInfo = ReadNetworkInfo(root, "sourceNetworkInfo", "source");
            var destNetworkInfo = ReadNetworkInfo(root, "destNetworkInfo", "dest", "destination");

            if (executionState is null)
                return ChainlinkMessageGetStatusResult.Fail("MESSAGE_NOT_FOUND", $"No message found for messageId '{messageID}', or the response shape did not match any known field name.");

            return ChainlinkMessageGetStatusResult.Ok(resolvedMessageID, executionState, sourceNetworkInfo, destNetworkInfo);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkApiException ex)
        {
            _logger.LogError(ex, "message/getStatus failed for {MessageId}", messageID);
            var (code, message) = ChainlinkErrorMapper.Map(ex);
            return ChainlinkMessageGetStatusResult.Fail(code, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "message/getStatus failed unexpectedly for {MessageId}", messageID);
            return ChainlinkMessageGetStatusResult.Fail("CCIP_API_UPSTREAM_ERROR", ex.Message);
        }
    }

    /// <summary>CCIP-MSG05 — message/checkManualExecutionRequired. Derived from getStatus (executionState == FAILURE); does not itself perform manual execution (00_INDEX.md §17 open question 5).</summary>
    public async Task<ChainlinkMessageCheckManualExecutionRequiredResult> CheckManualExecutionRequiredAsync(string messageID, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        var statusResult = await GetStatusAsync(messageID, credential, cancellationToken).ConfigureAwait(false);
        if (!statusResult.Success)
            return ChainlinkMessageCheckManualExecutionRequiredResult.Fail(statusResult.ErrorCode, statusResult.ErrorMessage);

        var manualExecutionRequired = string.Equals(statusResult.ExecutionState, "FAILURE", StringComparison.OrdinalIgnoreCase);
        return ChainlinkMessageCheckManualExecutionRequiredResult.Ok(manualExecutionRequired, statusResult.ExecutionState ?? string.Empty);
    }

    /// <summary>
    /// CCIP-MSG06 — message/getTokenTransferRateLimit. No confirmed real endpoint/ABI for this exists
    /// (00_INDEX.md §5.1 — the exact TokenPool rate-limiter getter name was never independently
    /// confirmed). Returns a prepared call descriptor (contract address, function name, args) for a
    /// downstream Ethereum SC01 node to actually execute, rather than guessing an unconfirmed ABI and
    /// silently returning a wrong or fabricated value.
    /// </summary>
    public Task<ChainlinkMessageGetTokenTransferRateLimitResult> GetTokenTransferRateLimitAsync(
        string tokenAddress, string destinationChainSelector, CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(destinationChainSelector, out _))
        {
            return Task.FromResult(ChainlinkMessageGetTokenTransferRateLimitResult.Fail(
                "INVALID_CHAIN_SELECTOR", $"'{destinationChainSelector}' is not a valid uint64 chain selector."));
        }

        var functionArgsJson = JsonSerializer.Serialize(new object[] { tokenAddress, destinationChainSelector });
        var result = ChainlinkMessageGetTokenTransferRateLimitResult.Ok(
            contractAddress: tokenAddress,
            functionName: "getCurrentOutboundRateLimiterState", // representative — NOT independently confirmed, 00_INDEX.md §17 open question 3
            functionArgsJson: functionArgsJson,
            rateLimitCapacity: null,
            rateLimitCurrent: null);
        return Task.FromResult(result);
    }

    private static string? ReadStringAny(JsonElement element, params string[] candidateNames)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return ChainlinkCcipApiClient.TryGetAny(element, out var value, candidateNames) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// Like <see cref="ReadStringAny"/>, but also accepts a JSON number and formats it as a string.
    /// The real CCIP API's exact field typing was never fully reconfirmed during design review (§17
    /// open question 13) — <c>chainId</c> in particular is a plausible candidate for being returned
    /// as a raw JSON number rather than a string (unlike <c>chainSelector</c>, which was directly
    /// confirmed as a string in the one real response fetched). Accepting either avoids silently
    /// losing the value if this assumption is wrong.
    /// </summary>
    private static string? ReadStringOrNumberAny(JsonElement element, params string[] candidateNames)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!ChainlinkCcipApiClient.TryGetAny(element, out var value, candidateNames)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static ChainlinkNetworkInfo? ReadNetworkInfo(JsonElement root, params string[] candidateNames)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!ChainlinkCcipApiClient.TryGetAny(root, out var networkElement, candidateNames)) return null;
        if (networkElement.ValueKind != JsonValueKind.Object) return null;

        var name = ReadStringAny(networkElement, "name", "networkName", "displayName") ?? string.Empty;
        var chainID = ReadStringOrNumberAny(networkElement, "chainId") ?? string.Empty;
        var chainSelector = ReadStringOrNumberAny(networkElement, "chainSelector") ?? string.Empty;
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(chainSelector)) return null;

        return new ChainlinkNetworkInfo(name, chainID, chainSelector);
    }
}
