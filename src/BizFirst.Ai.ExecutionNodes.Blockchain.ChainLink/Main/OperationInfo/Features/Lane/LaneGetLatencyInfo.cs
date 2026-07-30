namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-LNE02 — lane/getLatency.</summary>
internal sealed class LaneGetLatencyInfo : BaseChainlinkOperationInfo
{
    public string SourceChainSelector { get; private set; } = string.Empty;
    public string DestinationChainSelector { get; private set; } = string.Empty;

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        SourceChainSelector = reader.ReadConfigByKey("sourceChainSelector") ?? string.Empty;
        DestinationChainSelector = reader.ReadConfigByKey("destinationChainSelector") ?? string.Empty;
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["sourceChainSelector"] = SourceChainSelector,
        ["destinationChainSelector"] = DestinationChainSelector,
    };
}
