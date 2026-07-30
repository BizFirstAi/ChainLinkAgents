using BizFirst.Ai.AIExtension.Domain.Entities.CredentialParsers;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>
/// Optional CCIP API key vault resolution (00_INDEX.md §8). The CCIP API works unauthenticated for
/// the vast majority of its surface — the one gated feature (intents/quote) needs an API key, and an
/// API key also raises rate-limit headroom on the public endpoints. Read via
/// <c>ReadCredentialRawPrimaryAsync</c> + pattern-matching the real <see cref="ApiKeyRecord"/> vault
/// shape ("API_KEY"-type credential forms) — no custom credential-resolver service/interface is
/// introduced here.
///
/// This is a deliberate correction made during implementation, not what the original design proposed:
/// the design's own §8.2 sketched a bespoke <c>IChainlinkCredentialResolver</c>/
/// <c>ChainlinkCredentialResolver</c> service class, constructor-injected with the framework's
/// <c>ICredentialResolver</c>. No real BizFirst node actually does this — every sampled real
/// executor (Slack, SES, SqlServer, Binance) resolves credentials directly on the Scoped executor via
/// <c>ReadCredential*Async</c> and passes the resolved value down as a plain per-call parameter,
/// exactly like <c>BinanceNodeExecutor.Credentials.cs</c> does for its own two-part credential.
/// Introducing a bespoke resolver service here would have duplicated what the framework already
/// provides and — as this session's own review history for a different node found — risks a DI
/// captive-dependency bug if such a resolver were ever injected into a Singleton service instead of
/// read here on the Scoped executor.
/// </summary>
public sealed partial class ChainlinkNodeExecutor
{
    private async Task<ChainlinkApiCredential?> _ResolveCredentialAsync(CancellationToken cancellationToken)
    {
        var raw = await ReadCredentialRawPrimaryAsync(isMandatory: false, cancellationToken).ConfigureAwait(false);
        return raw is ApiKeyRecord apiKeyRecord && !string.IsNullOrWhiteSpace(apiKeyRecord.ApiKey)
            ? new ChainlinkApiCredential { ApiKey = apiKeyRecord.ApiKey }
            : null;
    }
}
