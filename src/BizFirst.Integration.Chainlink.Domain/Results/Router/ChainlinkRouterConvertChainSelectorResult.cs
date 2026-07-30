namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-RTR04 — router/convertChainSelector. Static lookup table conversion between a
/// network's native chain ID and its CCIP chain selector — the direct analogue of Wormhole's
/// WH-CHN03, purpose-built to prevent the exact three-way chain-ID confusion (native / Wormhole /
/// CCIP each have their own numbering for the same chain) described in 00_INDEX.md §4.6.
/// </summary>
public sealed record ChainlinkRouterConvertChainSelectorResult(
    bool Success,
    string? NetworkName,
    string? NativeChainID,
    string? ChainSelector,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkRouterConvertChainSelectorResult Ok(string networkName, string nativeChainID, string chainSelector) =>
        new(true, networkName, nativeChainID, chainSelector, string.Empty, string.Empty);

    public static ChainlinkRouterConvertChainSelectorResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, null, errorCode, errorMessage);
}
