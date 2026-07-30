namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-MSG06 — message/getTokenTransferRateLimit. On-chain read via the Router/TokenPool
/// rate limiter (delegated to the Ethereum ExecutionNode's own RPC path — this operation prepares the
/// contract address, function name, and ABI-ready args rather than duplicating Ethereum's on-chain
/// read capability; 00_INDEX.md §7.3/§15). <see cref="RateLimitCapacity"/> and
/// <see cref="RateLimitCurrent"/> are strings (uint128/uint256), not numeric types, for the same JS
/// safe-integer reason as every other on-chain integer in this node (00_INDEX.md §14.4 correction).
/// </summary>
public sealed record ChainlinkMessageGetTokenTransferRateLimitResult(
    bool Success,
    string? ContractAddress,
    string? FunctionName,
    string? FunctionArgsJson,
    string? RateLimitCapacity,
    string? RateLimitCurrent,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkMessageGetTokenTransferRateLimitResult Ok(
        string contractAddress, string functionName, string functionArgsJson, string? rateLimitCapacity, string? rateLimitCurrent) =>
        new(true, contractAddress, functionName, functionArgsJson, rateLimitCapacity, rateLimitCurrent, string.Empty, string.Empty);

    public static ChainlinkMessageGetTokenTransferRateLimitResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, null, null, null, errorCode, errorMessage);
}
