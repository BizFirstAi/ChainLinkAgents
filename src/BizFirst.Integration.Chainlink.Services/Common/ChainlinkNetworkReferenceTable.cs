namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Static per-network reference table: native chain ID, CCIP chain selector, and Router contract
/// address. Backs CCIP-RTR01 (router/getAddress) and CCIP-RTR04 (router/convertChainSelector).
///
/// Deliberately bundled as a small, explicitly-versioned static table rather than sourced from a
/// live directory API — 00_INDEX.md §17 open question 6 flags that the real CCIP Directory/
/// Configuration API v1's exact shape was never independently confirmed during design review. Only
/// Ethereum mainnet's chain selector was independently confirmed against Chainlink's own docs
/// (docs.chain.link/ccip/directory/mainnet/chain/mainnet, 00_INDEX.md §4.6); every other entry below
/// is a best-effort placeholder that MUST be reconciled against the live CCIP Directory before this
/// table is trusted for production traffic beyond Ethereum mainnet — see the "NOT INDEPENDENTLY
/// CONFIRMED" markers below. Router addresses in particular are per-CCIP-version and change when
/// Chainlink upgrades the Router contract; treat every non-Ethereum-mainnet row as a placeholder.
/// </summary>
public static class ChainlinkNetworkReferenceTable
{
    private static readonly IReadOnlyDictionary<string, ChainlinkNetworkReference> Entries =
        new Dictionary<string, ChainlinkNetworkReference>(StringComparer.OrdinalIgnoreCase)
        {
            // Chain selector independently confirmed against docs.chain.link/ccip/directory/mainnet
            // (00_INDEX.md §4.6). Router address is a REPRESENTATIVE placeholder — reconcile against
            // the live CCIP Directory before Phase 1A treats this as production-ready (§17 open
            // question 6).
            ["ethereum-mainnet"] = new ChainlinkNetworkReference(
                NetworkName: "ethereum-mainnet",
                NativeChainID: "1",
                ChainSelector: "5009297550715157269",
                RouterAddress: "0x80226fc0Ee2b096224EeAc085Bb9a8cba1146f7D"),

            // NOT INDEPENDENTLY CONFIRMED this pass — placeholder pending live CCIP Directory pull.
            ["arbitrum-mainnet"] = new ChainlinkNetworkReference(
                NetworkName: "arbitrum-mainnet",
                NativeChainID: "42161",
                ChainSelector: "4949039107694359620",
                RouterAddress: "0x141fa059441E0ca23ce184B6A78bafD2A517DdE8"),

            // NOT INDEPENDENTLY CONFIRMED this pass — placeholder pending live CCIP Directory pull.
            ["base-mainnet"] = new ChainlinkNetworkReference(
                NetworkName: "base-mainnet",
                NativeChainID: "8453",
                ChainSelector: "15971525489660198786",
                RouterAddress: "0x881e3A65B4d4a04dD529061dd0071cf975F58bCD"),
        };

    /// <summary>Looks up a network by name (case-insensitive). Returns null when not configured.</summary>
    public static ChainlinkNetworkReference? TryGetByNetworkName(string? networkName)
    {
        if (string.IsNullOrWhiteSpace(networkName)) return null;
        return Entries.TryGetValue(networkName, out var entry) ? entry : null;
    }

    /// <summary>Looks up a network by its CCIP chain selector (exact string match).</summary>
    public static ChainlinkNetworkReference? TryGetByChainSelector(string? chainSelector)
    {
        if (string.IsNullOrWhiteSpace(chainSelector)) return null;
        return Entries.Values.FirstOrDefault(e => e.ChainSelector == chainSelector);
    }

    /// <summary>Looks up a network by its native EVM chain ID (exact string match).</summary>
    public static ChainlinkNetworkReference? TryGetByNativeChainID(string? nativeChainID)
    {
        if (string.IsNullOrWhiteSpace(nativeChainID)) return null;
        return Entries.Values.FirstOrDefault(e => e.NativeChainID == nativeChainID);
    }
}
