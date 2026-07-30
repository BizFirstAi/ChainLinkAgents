namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>
/// DI registration for the Chainlink ExecutionNode plugin. Registers the Chainlink integration
/// services (rate-limit handler + CCIP API client + message builder + on-chain reader + the 3
/// resource services, via <see cref="ChainlinkServicesServiceCollectionExtensions.AddChainlinkIntegration"/>),
/// the executor, and the executor registry entry.
///
/// IMPORTANT — this alone is not sufficient to make the node discoverable at runtime. This
/// codebase's composition root (BizFirst.Ai.Platform.Web.Server.Core/DependencyInjection/Ai/
/// ServiceCollectionExtensionsForAI.cs, Plugins_RegisterAllNodes()) is a hardcoded list of
/// "new {X}Dependency().RegisterDefaults(services)" calls — a
/// "new ChainlinkDependency().RegisterDefaults(services);" line must be added there, alongside a
/// ProjectReference from that hosting project to this executor's csproj, or this node will build
/// but never register (confirmed real precedent: Binance/Ethereum/HashiCorp/IPFS are all wired in
/// exactly this way in that same file).
/// </summary>
public sealed class ChainlinkDependency : INodeExecutorDependency
{
    public void RegisterDefaults(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        // Bound lazily against IConfiguration resolved from DI when ChainlinkApiClientOptions is
        // first requested (the "Chainlink" section) — same deferred-binding pattern
        // BinanceApiClientOptions/EthereumNetworkOptions use. Overrides BaseURL and per-network RPC
        // URLs (Chainlink:NetworkRpcUrls:{network}) without a code change.
        services.AddOptions<ChainlinkApiClientOptions>().BindConfiguration("Chainlink");

        services.AddChainlinkIntegration();

        services.AddScoped<ChainlinkNodeExecutor>();

        ExecutorRegistry.Register<ChainlinkNodeExecutor>(ChainlinkNodeExecutor.NodeTypeName);
    }
}

/// <summary>Extension methods for manual DI registration.</summary>
public static class ChainlinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services required by the Chainlink executor node.
    /// <code>
    ///   builder.Services.AddChainlinkNodeExecutor();
    /// </code>
    /// </summary>
    public static IServiceCollection AddChainlinkNodeExecutor(this IServiceCollection services)
    {
        new ChainlinkDependency().RegisterDefaults(services);
        return services;
    }
}
