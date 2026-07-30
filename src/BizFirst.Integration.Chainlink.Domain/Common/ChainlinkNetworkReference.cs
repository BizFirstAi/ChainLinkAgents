namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// One row of the static per-network reference table (00_INDEX.md §5.2's <c>CCIP-RTR01</c>/
/// <c>CCIP-RTR04</c>): a network's native chain ID, CCIP chain selector, and Router contract address.
/// Deliberately NOT sourced from a live directory API this pass — 00_INDEX.md §17 open question 6
/// flags that the real CCIP Directory/Configuration API v1's exact shape was never independently
/// confirmed. Bundled here as a small, explicitly-versioned static table (see
/// <c>ChainlinkNetworkReferenceTable</c> in the Services project) so it can be corrected in one place
/// once that open question is resolved, rather than scattered across call sites.
/// </summary>
public sealed record ChainlinkNetworkReference(
    string NetworkName,
    string NativeChainID,
    string ChainSelector,
    string RouterAddress);
