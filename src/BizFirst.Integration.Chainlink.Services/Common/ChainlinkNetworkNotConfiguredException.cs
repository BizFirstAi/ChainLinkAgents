namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Thrown when a Router on-chain read is requested for a network with no configured RPC URL. Split
/// into its own file during a Guidelines-checklist validation pass — real precedent (BinanceApiException.cs,
/// BinanceIpBannedException.cs) puts exception types in their own files rather than bundled with the
/// class that throws them.
/// </summary>
public sealed class ChainlinkNetworkNotConfiguredException(string networkName)
    : Exception($"No RPC URL is configured for Chainlink network '{networkName}'. Configure 'Chainlink:NetworkRpcUrls:{networkName}'.")
{
    public string NetworkName { get; } = networkName;
}
