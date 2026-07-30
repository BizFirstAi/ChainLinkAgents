using Microsoft.Extensions.DependencyInjection;

namespace BizFirst.Integration.Chainlink.Services;

/// <summary>Extension methods to register all Chainlink integration services in the DI container.</summary>
public static class ChainlinkServicesServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Chainlink integration services: the rate-limit handler (Transient), the typed
    /// CCIP API HTTP client (factory-managed lifetime — never registered Scoped/Singleton itself,
    /// Guideline 10.2), the pure-computation message builder (Singleton — stateless, no I/O), the
    /// on-chain reader, and the three resource services (Scoped).
    /// </summary>
    public static IServiceCollection AddChainlinkIntegration(this IServiceCollection services)
    {
        services.AddTransient<ChainlinkRateLimitHandler>();

        services.AddHttpClient<ChainlinkCcipApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ChainlinkApiClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseURL);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<ChainlinkRateLimitHandler>();

        // Named HttpClient backing ChainlinkOnChainReader's cached Web3 clients (router/isChainSupported,
        // router/getFee) — IHttpClientFactory-managed instead of Nethereum's own unmanaged default
        // transport, and gives the RPC transport a bounded Timeout since Nethereum's Function.CallAsync
        // has no CancellationToken overload to cooperatively cancel a hanging call (see
        // ChainlinkOnChainReader's remarks).
        services.AddHttpClient(ChainlinkOnChainReader.RpcHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<ChainlinkMessageBuilder>();
        services.AddSingleton<ChainlinkOnChainReader>();

        services.AddScoped<ChainlinkMessageService>();
        services.AddScoped<ChainlinkRouterService>();
        services.AddScoped<ChainlinkLaneService>();

        return services;
    }
}
