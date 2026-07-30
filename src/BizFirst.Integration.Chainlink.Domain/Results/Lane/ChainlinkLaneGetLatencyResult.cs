namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-LNE02 — lane/getLatency. Backed by the real CCIP API's <c>GET /lanes</c> endpoint
/// (confirmed to exist as a named "Get Lane Latency" feature per docs.chain.link/ccip/tools/api,
/// 00_INDEX.md §5.3) — exact query parameters were not independently confirmed during design review
/// (§17 open question 1), so query-building is best-effort against the source+destination selectors.
/// </summary>
public sealed record ChainlinkLaneGetLatencyResult(
    bool Success,
    double? LatencyEstimateSeconds,
    string ErrorCode,
    string ErrorMessage)
{
    // Ok() deliberately takes a non-nullable double, even though the backing property is double? —
    // a Success result must always carry a real value (ChainlinkLaneService.GetLatencyAsync already
    // returns Fail() when no latency data was found, before ever calling Ok()), so there is no
    // legitimate "success but null" state. Fixed during a consistency review: the original signature
    // accepted double?, which forced every caller (the feature partial's output-building code) to
    // handle a null case that could never actually occur on the success path, via an ambiguous
    // "?? 0" fallback that couldn't be distinguished from a real zero-latency value.
    public static ChainlinkLaneGetLatencyResult Ok(double latencyEstimateSeconds) =>
        new(true, latencyEstimateSeconds, string.Empty, string.Empty);

    public static ChainlinkLaneGetLatencyResult Fail(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
