namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// CCIP API v2 client configuration. Bound from the "Chainlink" configuration section (see
/// ChainlinkDependency) so BaseURL and per-network RPC URLs can be overridden per-environment
/// without a code change — same deferred-binding pattern BinanceApiClientOptions uses.
/// </summary>
public sealed class ChainlinkApiClientOptions
{
    /// <summary>Base URL for the CCIP API v2. Confirmed against docs.chain.link/ccip/tools/api (00_INDEX.md §4.5).</summary>
    public string BaseURL { get; set; } = "https://api.ccip.chain.link/v2/";

    /// <summary>
    /// Per-network JSON-RPC endpoint URLs, keyed by network name (matching
    /// <see cref="ChainlinkNetworkReferenceTable"/>'s keys), used only by
    /// <see cref="ChainlinkOnChainReader"/> for the two Router operations that need a real on-chain
    /// read (isChainSupported, getFee — 00_INDEX.md §5.2). No production default is bundled here
    /// deliberately — unlike Binance's stable public REST root, an RPC endpoint is a
    /// deployment-specific secret/allocation (e.g. an Alchemy/Infura project URL) that must never be
    /// hardcoded as a source default.
    /// </summary>
    public Dictionary<string, string> NetworkRpcUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
