namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Thrown by <see cref="ChainlinkCcipApiClient"/> when the CCIP API returns a non-success
/// response, or when the underlying HTTP call fails outright (network error, timeout).
/// </summary>
public class ChainlinkApiException : Exception
{
    public int HttpStatusCode { get; }

    public ChainlinkApiException(string message, int httpStatusCode)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }
}
