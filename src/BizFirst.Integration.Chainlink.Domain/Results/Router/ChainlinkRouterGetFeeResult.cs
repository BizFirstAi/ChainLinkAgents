namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-RTR03 — router/getFee. Reads <c>IRouterClient.getFee(uint64, EVM2AnyMessage)</c>
/// directly against the resolved Router contract (00_INDEX.md §4.2's confirmed ABI), building the
/// same <c>EVM2AnyMessage</c> tuple CCIP-MSG01 builds. <see cref="FeeAmount"/> is a decimal string
/// (uint256 wei), not a numeric type, for the same JS safe-integer reason as every other on-chain
/// integer in this node.
///
/// <see cref="FeeToken"/> carries the RESOLVED address actually used for the fee calculation —
/// specifically the native-gas-token zero address (<c>0x0...0</c>) when the caller's config left
/// <c>feeToken</c> null/empty (00_INDEX.md's "empty = pay in native gas token" convention). Fixed
/// during a consistency review: an earlier version of the feature partial re-derived this field from
/// the raw config input instead of the value actually used on-chain, so an empty config value
/// produced a misleading empty output instead of the real zero-address value that was used.
/// </summary>
public sealed record ChainlinkRouterGetFeeResult(
    bool Success,
    string? FeeAmount,
    string? FeeToken,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkRouterGetFeeResult Ok(string feeAmount, string feeToken) =>
        new(true, feeAmount, feeToken, string.Empty, string.Empty);

    public static ChainlinkRouterGetFeeResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, errorCode, errorMessage);
}
