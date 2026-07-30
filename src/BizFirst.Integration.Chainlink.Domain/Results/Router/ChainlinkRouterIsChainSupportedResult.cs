namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-RTR02 — router/isChainSupported. Reads <c>IRouterClient.isChainSupported(uint64)</c>
/// directly against the resolved Router contract via a minimal, independent Nethereum on-chain read
/// (00_INDEX.md §4.2's confirmed ABI) — this project takes no C# ProjectReference to the Ethereum
/// ExecutionNode; it owns this one small, well-specified read itself rather than duplicating
/// Ethereum's generic SC01 abstraction.
/// </summary>
public sealed record ChainlinkRouterIsChainSupportedResult(
    bool Success,
    bool IsChainSupported,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkRouterIsChainSupportedResult Ok(bool isChainSupported) =>
        new(true, isChainSupported, string.Empty, string.Empty);

    public static ChainlinkRouterIsChainSupportedResult Fail(string errorCode, string errorMessage) =>
        new(false, false, errorCode, errorMessage);
}
