namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-RTR01 — router/getAddress. Static per-network reference table lookup, sourced
/// from the bundled <c>ChainlinkNetworkReferenceTable</c> (00_INDEX.md §5.2, §17 open question 6 —
/// not sourced from a live CCIP Directory API this pass).
/// </summary>
public sealed record ChainlinkRouterGetAddressResult(
    bool Success,
    string? RouterAddress,
    string? ChainSelector,
    string? NativeChainID,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkRouterGetAddressResult Ok(string routerAddress, string chainSelector, string nativeChainID) =>
        new(true, routerAddress, chainSelector, nativeChainID, string.Empty, string.Empty);

    public static ChainlinkRouterGetAddressResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, null, errorCode, errorMessage);
}
