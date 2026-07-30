namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-LNE03 — lane/getRiskManagementStatus. Backed by the real CCIP API's
/// <c>GET /verifiers</c> endpoint — "verifiers" is CCIP's own term for the Risk Management Network's
/// blessing role (00_INDEX.md §4.1/§4.5/§5.3, upgraded from "representative" once the real endpoint
/// was discovered during design review). <see cref="RiskManagementStatus"/> is a short status string
/// (e.g. <c>"NORMAL"</c>/<c>"ANOMALY_DETECTED"</c>) rather than a boolean, since the real response
/// shape was not independently confirmed to be binary.
/// </summary>
public sealed record ChainlinkLaneGetRiskManagementStatusResult(
    bool Success,
    string? RiskManagementStatus,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkLaneGetRiskManagementStatusResult Ok(string? riskManagementStatus) =>
        new(true, riskManagementStatus, string.Empty, string.Empty);

    public static ChainlinkLaneGetRiskManagementStatusResult Fail(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
