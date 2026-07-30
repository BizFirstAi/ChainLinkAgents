using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using System.Numerics;

namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Minimal, independent on-chain reader for the two Router operations whose own output contract
/// (00_INDEX.md §14.4) promises a real resolved value — <c>isChainSupported</c> (bool) and
/// <c>getFee</c> (a fee amount) — rather than a prepared call descriptor. Uses Nethereum directly
/// (already a repo-wide dependency, same package the Ethereum ExecutionNode uses) with a
/// hand-written minimal ABI fragment for <c>IRouterClient</c>, confirmed against the real Solidity
/// interface in 00_INDEX.md §4.2.
///
/// Deliberate scope decision made during implementation, not fully settled by the design doc: the
/// design repeatedly states this node takes "no C# ProjectReference to Ethereum's Services project"
/// and "never reaches into Ethereum's services at the code level" (00_INDEX.md §7.3), which this
/// class honors (no ProjectReference to BizFirst.Integration.Ethereum.Services anywhere in this
/// project). But §15's own workflow example also says GetFee "delegates internally to Ethereum SC01"
/// — worded ambiguously between "prepare inputs for a separate Ethereum SC01 node" and "make the read
/// itself." Given §14.4 explicitly promises a real <c>isChainSupported</c>/<c>feeAmount</c> value as
/// this node's own output (not a call descriptor), this implementation resolves the ambiguity in
/// favor of actually performing the read — via a small, independent Nethereum Web3 client, never
/// through Ethereum's own C# code.
///
/// No wallet, no signing, no private key material anywhere in this class — read-only <c>eth_call</c>
/// only, consistent with 00_INDEX.md §7.3/§8's "zero private keys" design.
/// </summary>
public sealed class ChainlinkOnChainReader
{
    // Hand-written minimal ABI fragment for IRouterClient — confirmed against 00_INDEX.md §4.2's
    // real Solidity interface (isChainSupported/getFee), not the full Router ABI.
    private const string RouterClientAbi = """
        [
          {
            "type":"function","name":"isChainSupported","stateMutability":"view",
            "inputs":[{"name":"destChainSelector","type":"uint64"}],
            "outputs":[{"name":"supported","type":"bool"}]
          },
          {
            "type":"function","name":"getFee","stateMutability":"view",
            "inputs":[
              {"name":"destinationChainSelector","type":"uint64"},
              {"name":"message","type":"tuple","components":[
                {"name":"receiver","type":"bytes"},
                {"name":"data","type":"bytes"},
                {"name":"tokenAmounts","type":"tuple[]","components":[
                  {"name":"token","type":"address"},
                  {"name":"amount","type":"uint256"}
                ]},
                {"name":"feeToken","type":"address"},
                {"name":"extraArgs","type":"bytes"}
              ]}
            ],
            "outputs":[{"name":"fee","type":"uint256"}]
          }
        ]
        """;

    /// <summary>Named HttpClient used for the RPC transport — registered via IHttpClientFactory (ChainlinkDependency) so pooling and a bounded Timeout are inherited, instead of each cached Web3 instance building its own unmanaged HttpClient internally.</summary>
    public const string RpcHttpClientName = "Chainlink.Rpc";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChainlinkApiClientOptions _options;
    private readonly ILogger<ChainlinkOnChainReader> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IWeb3> _clients = new(StringComparer.OrdinalIgnoreCase);

    public ChainlinkOnChainReader(IHttpClientFactory httpClientFactory, IOptions<ChainlinkApiClientOptions> options, ILogger<ChainlinkOnChainReader> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <remarks>
    /// <paramref name="cancellationToken"/> is honored only up front (a pre-cancelled token returns
    /// immediately) — confirmed during implementation via reflection against the real Nethereum 6.0.0
    /// <c>Function.CallAsync&lt;TReturn&gt;</c> API that NONE of its overloads accept a
    /// <see cref="CancellationToken"/>, so a token cancelled mid-flight cannot abort an in-progress RPC
    /// call at this layer. Resilience against a hanging/unresponsive RPC endpoint instead comes from
    /// the bounded <see cref="HttpClient.Timeout"/> configured on <see cref="RpcHttpClientName"/>
    /// (<c>ChainlinkServicesServiceCollectionExtensions</c>) rather than cooperative cancellation.
    /// </remarks>
    public async Task<bool> IsChainSupportedAsync(string networkName, string routerAddress, string destinationChainSelector, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var web3 = GetClient(networkName);
        var function = web3.Eth.GetContract(RouterClientAbi, routerAddress).GetFunction("isChainSupported");
        return await function.CallAsync<bool>(ParseUlongChainSelector(destinationChainSelector)).ConfigureAwait(false);
    }

    /// <remarks>See <see cref="IsChainSupportedAsync"/>'s remarks — the same CancellationToken/timeout tradeoff applies here.</remarks>
    public async Task<string> GetFeeAsync(
        string networkName, string routerAddress, string destinationChainSelector,
        string receiverBytesHex, string dataBytesHex, IReadOnlyList<ChainlinkTokenAmount> tokenAmounts,
        string feeTokenAddress, string extraArgsBytesHex, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var web3 = GetClient(networkName);
        var function = web3.Eth.GetContract(RouterClientAbi, routerAddress).GetFunction("getFee");

        object[] tokenAmountsArg = tokenAmounts
            .Select(t => new object[] { t.Token, BigInteger.Parse(t.Amount, CultureInfo.InvariantCulture) })
            .ToArray();

        object[] messageTuple =
        {
            receiverBytesHex.HexToByteArray(),
            dataBytesHex.HexToByteArray(),
            tokenAmountsArg,
            feeTokenAddress,
            extraArgsBytesHex.HexToByteArray(),
        };

        var fee = await function.CallAsync<BigInteger>(ParseUlongChainSelector(destinationChainSelector), messageTuple).ConfigureAwait(false);
        return fee.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Builds (and caches, for the Singleton's lifetime) a Web3 client per network, backed by an
    /// <see cref="IHttpClientFactory"/>-managed <see cref="HttpClient"/> rather than Nethereum's own
    /// default internal transport — confirmed via a standalone check during implementation that
    /// Nethereum's <c>RpcClient</c> accepts an externally-supplied <see cref="HttpClient"/>
    /// constructor overload. This gives the RPC transport a bounded, configured <c>Timeout</c> (see
    /// <c>ChainlinkServicesServiceCollectionExtensions</c>) instead of whatever Nethereum's own internal HttpClient
    /// defaults to.
    /// </summary>
    private IWeb3 GetClient(string networkName)
    {
        return _clients.GetOrAdd(networkName, name =>
        {
            if (!_options.NetworkRpcUrls.TryGetValue(name, out var rpcUrl) || string.IsNullOrWhiteSpace(rpcUrl))
            {
                throw new ChainlinkNetworkNotConfiguredException(name);
            }

            var httpClient = _httpClientFactory.CreateClient(RpcHttpClientName);
            var rpcClient = new Nethereum.JsonRpc.Client.RpcClient(new Uri(rpcUrl), httpClient, authHeaderValue: null, jsonSerializerSettings: null, log: _logger);
            return new Web3(rpcClient);
        });
    }

    /// <summary>
    /// CCIP chain selectors are Solidity uint64 — parsed as ulong (not long), matching the real
    /// on-chain type exactly. Values are carried as strings everywhere else in this node (00_INDEX.md
    /// §13.2's precision correction) specifically to avoid JS number-precision loss; this is the one
    /// place the string is converted back to its real numeric type, right before an actual on-chain
    /// call — not a moment sooner.
    /// </summary>
    private static ulong ParseUlongChainSelector(string chainSelector) =>
        ulong.Parse(chainSelector, CultureInfo.InvariantCulture);
}
