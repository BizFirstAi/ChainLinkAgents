namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-MSG06 — message/getTokenTransferRateLimit.</summary>
internal sealed class MessageGetTokenTransferRateLimitInfo : BaseChainlinkOperationInfo
{
    public string TokenAddress { get; private set; } = string.Empty;
    public string DestinationChainSelector { get; private set; } = string.Empty;

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        TokenAddress = reader.ReadConfigByKey("tokenAddress") ?? string.Empty;
        DestinationChainSelector = reader.ReadConfigByKey("destinationChainSelector") ?? string.Empty;
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["tokenAddress"] = TokenAddress,
        ["destinationChainSelector"] = DestinationChainSelector,
    };
}
