namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// One CCIP lane — a specific (source chain, destination chain) pair with its own configured
/// OnRamp/OffRamp. Not every chain pair CCIP individually supports is necessarily a supported lane
/// between each other (00_INDEX.md §5.3).
/// </summary>
public sealed record ChainlinkLane(
    string SourceNetworkName,
    string SourceChainSelector,
    string DestinationNetworkName,
    string DestinationChainSelector);
