namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Maps a failed CCIP API call to one of the CHAINLINK_* error codes from the design spec (§16).
/// Not an exhaustive list — the design's own §16 explicitly says to expand this during per-operation
/// implementation, following the pattern (not an exact fixed code set).
/// </summary>
public static class ChainlinkErrorMapper
{
    public static (string ErrorCode, string Message) Map(ChainlinkApiException ex) =>
        ex.HttpStatusCode switch
        {
            404 => ("MESSAGE_NOT_FOUND", ex.Message),
            429 => ("CCIP_API_RATE_LIMITED", ex.Message),
            502 or 500 or 503 => ("CCIP_API_UPSTREAM_ERROR", ex.Message),
            504 => ("CCIP_API_UPSTREAM_ERROR", ex.Message),
            _ => ("CCIP_API_UPSTREAM_ERROR", ex.Message),
        };
}
