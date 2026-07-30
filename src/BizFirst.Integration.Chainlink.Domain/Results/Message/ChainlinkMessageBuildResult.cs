namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-MSG01 — message/build. Local, pure computation — no HTTP, no RPC. Assembles the
/// ABI-encoded <c>Client.EVM2AnyMessage</c> tuple and the tag-prefixed <c>extraArgs</c> bytes.
/// <see cref="EstimatedMessageID"/> is deliberately always null — the real <c>messageId</c> is only
/// assigned once <c>ccipSend</c> actually executes on-chain (delegated to the Ethereum ExecutionNode's
/// SC02, 00_INDEX.md §15); this operation cannot predict it.
/// </summary>
public sealed record ChainlinkMessageBuildResult(
    bool Success,
    string? EncodedMessage,
    string? ExtraArgsBytes,
    string? EstimatedMessageID,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkMessageBuildResult Ok(string encodedMessage, string extraArgsBytes) =>
        new(true, encodedMessage, extraArgsBytes, null, string.Empty, string.Empty);

    public static ChainlinkMessageBuildResult Fail(string errorCode, string errorMessage) =>
        new(false, null, null, null, errorCode, errorMessage);
}
