namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>
/// CCIP-LNE01 — lane/getSupportedLanes. <c>network</c> is optional — omit for all lanes.
/// Config key deliberately named <c>network</c>, not <c>sourceNetworkName</c> (fixed during a
/// consistency review) — every other operation that identifies a single network
/// (router/getAddress, router/isChainSupported, router/getFee, router/convertChainSelector) uses
/// the config key <c>network</c>; this operation has no accompanying "destination" key on the same
/// operation to disambiguate against, so there was no reason for it to diverge. The C# property
/// stays <see cref="SourceNetworkName"/> (not renamed) since it legitimately needs to match
/// <see cref="ChainlinkLane.SourceNetworkName"/> and the per-lane <c>sourceNetworkName</c> output
/// field, both of which correctly distinguish source from destination.
/// </summary>
internal sealed class LaneGetSupportedLanesInfo : BaseChainlinkOperationInfo
{
    public string? SourceNetworkName { get; private set; }

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        SourceNetworkName = reader.ReadConfigByKeyDefaultNull("network");
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["network"] = SourceNetworkName ?? string.Empty,
    };
}
