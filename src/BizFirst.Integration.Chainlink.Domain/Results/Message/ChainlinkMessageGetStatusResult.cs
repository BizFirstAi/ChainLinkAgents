namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-MSG02 — message/getStatus. HTTP: <c>GET /messages/:messageId</c> against
/// api.ccip.chain.link/v2. <see cref="ExecutionState"/> is kept as the exact on-chain
/// <c>MessageExecutionState</c> enum name (<c>UNTOUCHED</c>/<c>IN_PROGRESS</c>/<c>SUCCESS</c>/
/// <c>FAILURE</c>) rather than remapped to a BizFirst-specific vocabulary, since a workflow author
/// cross-referencing the CCIP Explorer needs the same term.
/// </summary>
public sealed record ChainlinkMessageGetStatusResult(
    bool Success,
    string? MessageID,
    string? ExecutionState,
    ChainlinkNetworkInfo? SourceNetworkInfo,
    ChainlinkNetworkInfo? DestNetworkInfo,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkMessageGetStatusResult Ok(
        string messageID, string executionState, ChainlinkNetworkInfo? sourceNetworkInfo, ChainlinkNetworkInfo? destNetworkInfo) =>
        new(true, messageID, executionState, sourceNetworkInfo, destNetworkInfo, string.Empty, string.Empty);

    public static ChainlinkMessageGetStatusResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, null, null, errorCode, errorMessage);
}
