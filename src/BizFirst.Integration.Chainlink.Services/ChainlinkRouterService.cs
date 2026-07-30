namespace BizFirst.Integration.Chainlink.Services;

/// <summary>Router resource: getAddress/convertChainSelector (static lookup), isChainSupported/getFee (real on-chain reads via ChainlinkOnChainReader).</summary>
public sealed class ChainlinkRouterService
{
    private readonly ChainlinkOnChainReader _onChainReader;
    private readonly ChainlinkMessageBuilder _messageBuilder;
    private readonly ILogger<ChainlinkRouterService> _logger;

    public ChainlinkRouterService(ChainlinkOnChainReader onChainReader, ChainlinkMessageBuilder messageBuilder, ILogger<ChainlinkRouterService> logger)
    {
        _onChainReader = onChainReader ?? throw new ArgumentNullException(nameof(onChainReader));
        _messageBuilder = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>CCIP-RTR01 — router/getAddress. Static per-network reference table lookup.</summary>
    public ChainlinkRouterGetAddressResult GetAddress(string networkName)
    {
        var entry = ChainlinkNetworkReferenceTable.TryGetByNetworkName(networkName);
        return entry is null
            ? ChainlinkRouterGetAddressResult.Fail("ROUTER_NOT_FOUND", $"No known Router address for network '{networkName}'.")
            : ChainlinkRouterGetAddressResult.Ok(entry.RouterAddress, entry.ChainSelector, entry.NativeChainID);
    }

    /// <summary>CCIP-RTR02 — router/isChainSupported. Real on-chain read against the resolved Router contract.</summary>
    public async Task<ChainlinkRouterIsChainSupportedResult> IsChainSupportedAsync(
        string networkName, string destinationChainSelector, CancellationToken cancellationToken = default)
    {
        var entry = ChainlinkNetworkReferenceTable.TryGetByNetworkName(networkName);
        if (entry is null)
            return ChainlinkRouterIsChainSupportedResult.Fail("ROUTER_NOT_FOUND", $"No known Router address for network '{networkName}'.");

        if (!ulong.TryParse(destinationChainSelector, out _))
            return ChainlinkRouterIsChainSupportedResult.Fail("INVALID_CHAIN_SELECTOR", $"'{destinationChainSelector}' is not a valid uint64 chain selector.");

        try
        {
            var supported = await _onChainReader.IsChainSupportedAsync(networkName, entry.RouterAddress, destinationChainSelector, cancellationToken).ConfigureAwait(false);
            return ChainlinkRouterIsChainSupportedResult.Ok(supported);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkNetworkNotConfiguredException ex)
        {
            return ChainlinkRouterIsChainSupportedResult.Fail("NETWORK_NOT_CONFIGURED", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "router/isChainSupported failed for {Network} -> {ChainSelector}", networkName, destinationChainSelector);
            return ChainlinkRouterIsChainSupportedResult.Fail("ONCHAIN_READ_ERROR", ex.Message);
        }
    }

    /// <summary>CCIP-RTR03 — router/getFee. Builds the same EVM2AnyMessage tuple CCIP-MSG01 builds, then reads getFee on-chain.</summary>
    public async Task<ChainlinkRouterGetFeeResult> GetFeeAsync(
        string networkName, string destinationChainSelector, string receiverAddress, string? payloadDataHex,
        IReadOnlyList<ChainlinkTokenAmount> tokenAmounts, string? feeToken, string? extraArgsVersion,
        long? gasLimit, bool allowOutOfOrderExecution, CancellationToken cancellationToken = default)
    {
        var entry = ChainlinkNetworkReferenceTable.TryGetByNetworkName(networkName);
        if (entry is null)
            return ChainlinkRouterGetFeeResult.Fail("ROUTER_NOT_FOUND", $"No known Router address for network '{networkName}'.");

        if (!ulong.TryParse(destinationChainSelector, out _))
            return ChainlinkRouterGetFeeResult.Fail("INVALID_CHAIN_SELECTOR", $"'{destinationChainSelector}' is not a valid uint64 chain selector.");

        ChainlinkMessageBuilder.MessageParts parts;
        try
        {
            parts = _messageBuilder.BuildParts(receiverAddress, payloadDataHex, tokenAmounts, feeToken, extraArgsVersion, gasLimit, allowOutOfOrderExecution);
        }
        catch (ArgumentException ex)
        {
            // Same ParamName-based mapping as ChainlinkMessageService.Build — a bad address/amount/
            // extraArgsVersion here is a validation failure, not an on-chain/upstream error, and must
            // not be reported as CCIP_API_UPSTREAM_ERROR.
            var errorCode = ex.ParamName switch
            {
                nameof(receiverAddress) or nameof(feeToken) => "INVALID_ADDRESS",
                "tokenAmounts" => "INVALID_TOKEN_AMOUNT",
                nameof(gasLimit) => "INVALID_GAS_LIMIT",
                _ => "INVALID_EXTRA_ARGS_VERSION",
            };
            return ChainlinkRouterGetFeeResult.Fail(errorCode, ex.Message);
        }

        try
        {
            var feeAmount = await _onChainReader.GetFeeAsync(
                networkName, entry.RouterAddress, destinationChainSelector,
                parts.ReceiverBytesHex, parts.DataBytesHex, parts.TokenAmounts, parts.FeeTokenAddress, parts.ExtraArgsBytesHex,
                cancellationToken).ConfigureAwait(false);

            return ChainlinkRouterGetFeeResult.Ok(feeAmount, parts.FeeTokenAddress);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkNetworkNotConfiguredException ex)
        {
            return ChainlinkRouterGetFeeResult.Fail("NETWORK_NOT_CONFIGURED", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "router/getFee failed for {Network} -> {ChainSelector}", networkName, destinationChainSelector);
            return ChainlinkRouterGetFeeResult.Fail("ONCHAIN_READ_ERROR", ex.Message);
        }
    }

    /// <summary>CCIP-RTR04 — router/convertChainSelector. Static lookup table conversion, direction inferred from which input is supplied.</summary>
    public ChainlinkRouterConvertChainSelectorResult ConvertChainSelector(string? networkName, string? nativeChainID, string? chainSelector)
    {
        var entry = ChainlinkNetworkReferenceTable.TryGetByNetworkName(networkName)
            ?? ChainlinkNetworkReferenceTable.TryGetByNativeChainID(nativeChainID)
            ?? ChainlinkNetworkReferenceTable.TryGetByChainSelector(chainSelector);

        return entry is null
            ? ChainlinkRouterConvertChainSelectorResult.Fail("INVALID_CHAIN_SELECTOR", "No known network matches the supplied networkName/nativeChainId/chainSelector.")
            : ChainlinkRouterConvertChainSelectorResult.Ok(entry.NetworkName, entry.NativeChainID, entry.ChainSelector);
    }
}
