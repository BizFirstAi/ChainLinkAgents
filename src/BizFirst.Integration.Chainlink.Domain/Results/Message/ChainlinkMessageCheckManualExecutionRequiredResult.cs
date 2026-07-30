namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-MSG05 — message/checkManualExecutionRequired. Derived from a getStatus call
/// (<c>executionState == FAILURE</c>) — reports whether the message is eligible for manual
/// re-execution. Does NOT itself perform manual execution (00_INDEX.md §17 open question 5 — the
/// real authorization/calldata shape for a manual-execution write was never confirmed).
/// </summary>
public sealed record ChainlinkMessageCheckManualExecutionRequiredResult(
    bool Success,
    bool ManualExecutionRequired,
    string? ExecutionState,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkMessageCheckManualExecutionRequiredResult Ok(bool manualExecutionRequired, string executionState) =>
        new(true, manualExecutionRequired, executionState, string.Empty, string.Empty);

    public static ChainlinkMessageCheckManualExecutionRequiredResult Fail(string errorCode, string errorMessage) =>
        new(false, false, null, errorCode, errorMessage);
}
