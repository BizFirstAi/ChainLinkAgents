namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-RTR04 — router/convertChainSelector. Supply exactly one of the three lookup keys; whichever is supplied determines the lookup direction.</summary>
internal sealed class RouterConvertChainSelectorInfo : BaseChainlinkOperationInfo
{
    public string? NetworkName { get; private set; }
    public string? NativeChainID { get; private set; }
    public string? ChainSelector { get; private set; }

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        NetworkName = reader.ReadConfigByKeyDefaultNull("network");
        NativeChainID = reader.ReadConfigByKeyDefaultNull("nativeChainId");
        ChainSelector = reader.ReadConfigByKeyDefaultNull("chainSelector");
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["network"] = NetworkName ?? string.Empty,
        ["nativeChainId"] = NativeChainID ?? string.Empty,
        ["chainSelector"] = ChainSelector ?? string.Empty,
    };
}
