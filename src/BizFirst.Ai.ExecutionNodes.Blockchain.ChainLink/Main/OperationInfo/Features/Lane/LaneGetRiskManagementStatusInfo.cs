namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-LNE03 — lane/getRiskManagementStatus. chainSelector is optional — omit for global status.</summary>
internal sealed class LaneGetRiskManagementStatusInfo : BaseChainlinkOperationInfo
{
    public string? ChainSelector { get; private set; }

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        ChainSelector = reader.ReadConfigByKeyDefaultNull("chainSelector");
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["chainSelector"] = ChainSelector ?? string.Empty,
    };
}
