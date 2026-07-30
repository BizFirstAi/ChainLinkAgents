namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Resolved CCIP API key. Never held as state on any Singleton — resolved once by the executor
/// (via <c>ReadCredentialRawPrimaryAsync</c>, matching the real <c>ApiKeyRecord</c> vault shape used
/// across this codebase) and passed down as a plain value on every call, the same way
/// BinanceApiCredential is passed to BinanceApiClient/BinanceServices — never injected into a
/// Singleton service.
/// </summary>
public sealed class ChainlinkApiCredential
{
    public required string ApiKey { get; init; }
}
