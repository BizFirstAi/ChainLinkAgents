namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-RTR01 — router/getAddress.</summary>
internal sealed class RouterGetAddressInfo : BaseChainlinkOperationInfo
{
    public string NetworkName { get; private set; } = string.Empty;

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        NetworkName = reader.ReadConfigByKey("network", "ethereum-mainnet") ?? "ethereum-mainnet";
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["network"] = NetworkName,
    };
}
