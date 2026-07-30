namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-RTR02 — router/isChainSupported.</summary>
internal sealed class RouterIsChainSupportedInfo : BaseChainlinkOperationInfo
{
    public string NetworkName { get; private set; } = string.Empty;
    public string DestinationChainSelector { get; private set; } = string.Empty;

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        NetworkName = reader.ReadConfigByKey("network", "ethereum-mainnet") ?? "ethereum-mainnet";
        DestinationChainSelector = reader.ReadConfigByKey("destinationChainSelector") ?? string.Empty;
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["network"] = NetworkName,
        ["destinationChainSelector"] = DestinationChainSelector,
    };
}
