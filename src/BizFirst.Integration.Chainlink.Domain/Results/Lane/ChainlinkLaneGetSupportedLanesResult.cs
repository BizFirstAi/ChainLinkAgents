namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Result of CCIP-LNE01 — lane/getSupportedLanes. Backed by the real CCIP API's <c>GET /chains</c>
/// endpoint (00_INDEX.md §4.5/§5.3, upgraded from "representative" during design review once the
/// real endpoint list was fetched) restricted to a specific source network when one is supplied.
/// </summary>
public sealed record ChainlinkLaneGetSupportedLanesResult(
    bool Success,
    IReadOnlyList<ChainlinkLane> Lanes,
    string ErrorCode,
    string ErrorMessage)
{
    public static ChainlinkLaneGetSupportedLanesResult Ok(IReadOnlyList<ChainlinkLane> lanes) =>
        new(true, lanes, string.Empty, string.Empty);

    public static ChainlinkLaneGetSupportedLanesResult Fail(string errorCode, string errorMessage) =>
        new(false, Array.Empty<ChainlinkLane>(), errorCode, errorMessage);
}
