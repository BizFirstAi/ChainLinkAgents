# ChainLinkAgents

[![BizFirst.Ai](https://www.bizfirstai.com/website/assets/Logo/logo.png)](https://bizfirstai.com)

Chainlink community node for [BizFirst.Ai](https://bizfirstai.com) — a ProcessEngine `ExecutionNode`
(`chainlink`) that exposes Chainlink's **CCIP (Cross-Chain Interoperability Protocol)** as drag-and-drop steps
in [BizFirst.Ai](https://bizfirstai.com) workflow automations: building cross-chain messages, tracking their
delivery status, reading Router contract state, and checking Lane/Risk Management Network health.

## What it does

`ChainLinkAgents` lets a BizFirst.Ai workflow build, send-prepare, and monitor Chainlink CCIP cross-chain
messages without touching an SDK. `message/build` does pure local ABI encoding (no network call at all); the
HTTP operations call the free, public **CCIP API v2** (`api.ccip.chain.link/v2`); and
`router/isChainSupported`/`router/getFee` make a real, independent on-chain read via a minimal Nethereum client.
No operation on this node holds a wallet or signs a transaction — actually sending a built message
(`ccipSend(...)`) is a separate step performed by the Ethereum ExecutionNode's own contract-write operation.

| Resource | Operation | Description |
|---|---|---|
| `message` | `build` | Builds the ABI-encoded `EVM2AnyMessage` tuple + `extraArgs` bytes. Local, pure computation. |
| `message` | `getStatus` | Looks up a sent message's delivery status (`UNTOUCHED`/`IN_PROGRESS`/`SUCCESS`/`FAILURE`). |
| `message` | `checkManualExecutionRequired` | Reports whether a `FAILURE`-state message is eligible for manual re-execution. |
| `message` | `getTokenTransferRateLimit` | Prepares a call descriptor for a token's cross-chain rate limit (unresolved — ABI unconfirmed). |
| `router` | `getAddress` | Static per-network Router contract address lookup. |
| `router` | `isChainSupported` | Real on-chain read: is a destination chain supported by this Router. |
| `router` | `getFee` | Real on-chain read: the CCIP fee for a given message. |
| `router` | `convertChainSelector` | Converts between network name, native chain ID, and CCIP chain selector. |
| `lane` | `getSupportedLanes` | Lists supported (source, destination) CCIP lane pairs. |
| `lane` | `getLatency` | Estimated message latency for a specific lane. |
| `lane` | `getRiskManagementStatus` | Risk Management Network ("verifiers") blessing status. |

This node covers **CCIP only**. Chainlink Data Feeds are out of scope (delegated to the Ethereum ExecutionNode's
generic contract-call path + Standards Registry); VRF, Automation, and Functions are not implemented by any node
in this codebase. See [Roadmap](https://docs.bizfirstai.com/Nodes/ChainLink/10-roadmap.html).

## Source Code

Browse the real implementation in [`src/`](src/) — three .NET projects, copied verbatim from the BizFirst.Ai
platform, no code or namespace changes:

- [`src/BizFirst.Integration.Chainlink.Domain`](src/BizFirst.Integration.Chainlink.Domain) — result records + shared value types (zero deps)
- [`src/BizFirst.Integration.Chainlink.Services`](src/BizFirst.Integration.Chainlink.Services) — CCIP API v2 client, ABI encoder, on-chain reader, resource services
- [`src/BizFirst.Ai.ExecutionNodes.Blockchain.ChainLink`](src/BizFirst.Ai.ExecutionNodes.Blockchain.ChainLink) — the executor: routing, config parsing, operation DTOs

## Documentation

- **This site:** [chainlink.bizfirstai.com](https://chainlink.bizfirstai.com) — quick reference and links
- **Full guide (11 pages):** [docs.bizfirstai.com/Nodes/ChainLink](https://docs.bizfirstai.com/Nodes/ChainLink/) —
  configuration, networks, every resource's operations, CCIP concepts, examples, troubleshooting
- **Full developer portal:** [docs.bizfirstai.com](https://docs.bizfirstai.com)

All BizFirst.Ai node documentation is maintained in one place — the
[UserGuides](https://github.com/BizFirstAi/UserGuides) portal — rather than duplicated per repo.

## Project layout

```
src/
├── BizFirst.Integration.Chainlink.Domain             # Result records + shared value types (zero deps)
├── BizFirst.Integration.Chainlink.Services            # CCIP API v2 client, ABI encoder, on-chain reader, resource services
└── BizFirst.Ai.ExecutionNodes.Blockchain.ChainLink     # Executor: routing, config, operation DTOs
docs/
├── index.html  # This site's homepage — quick reference, links out to the full guide
└── CNAME       # chainlink.bizfirstai.com
```

Targets **.NET 9**. The `Tests` project (unit/regression tests, 44 tests as of the last review pass) is part of
the source platform's repo but is not included here.

> **Naming note:** the two integration projects use the lowercase spelling `Chainlink`
> (`BizFirst.Integration.Chainlink.Domain`/`.Services`), while the executor project and this repo use
> `ChainLink`. Both spellings refer to the exact same node.

## Configuration

```json
"Chainlink": {
  "BaseUrl": "https://api.ccip.chain.link/v2/",
  "NetworkRpcUrls": {
    "ethereum-mainnet": "https://your-rpc-provider/...",
    "arbitrum-mainnet": "https://your-rpc-provider/...",
    "base-mainnet": "https://your-rpc-provider/..."
  }
}
```

`BaseUrl` defaults to the real public CCIP API and rarely needs overriding. `NetworkRpcUrls` is required only
for `router/isChainSupported` and `router/getFee` — every other operation works with no RPC configuration at
all. No production default RPC URL is bundled, since an RPC endpoint is deployment-specific infrastructure. An
optional CCIP API key (an `API_KEY`-type vault credential) raises rate-limit headroom but is not required for
any operation this node implements.

## Registration

`ChainlinkDependency.RegisterDefaults(services)` registers the CCIP API client, rate-limit handler, message
builder, on-chain reader, the 3 resource services, the executor (scoped), and the `ExecutorRegistry` entry
(`chainlink`). Host applications should also add `new ChainlinkDependency().RegisterDefaults(services);` to
their node-plugin bootstrap (`Plugins_RegisterAllNodes()` in `ServiceCollectionExtensionsForAI.cs`), alongside a
`ProjectReference` to this executor's csproj, so the assembly is force-loaded and discoverable at runtime.

## Roadmap

- **Manual execution submission** — `checkManualExecutionRequired` only reports eligibility today; actually
  submitting a manual execution needs a confirmed authorization/calldata shape.
- **Live CCIP Directory integration** — the per-network reference table is a small bundled static table today;
  only Ethereum mainnet's chain selector is independently confirmed, and every Router address is a placeholder.
- **`getTokenTransferRateLimit` resolution** — currently returns a prepared call descriptor since no on-chain
  rate-limiter ABI was confirmed during design review.
- Two originally-designed operations (get message by transaction, list messages by address) were cut — no real
  CCIP API endpoint was found to back either.

See [Roadmap](https://docs.bizfirstai.com/Nodes/ChainLink/10-roadmap.html) for full detail.

## About BizFirst.Ai

[BizFirst.Ai](https://bizfirstai.com) is a workflow automation platform for building AI-driven business
processes. This node is one of many community connectors that plug into its ProcessEngine — browse the full
node catalogue and developer guides at [docs.bizfirstai.com](https://docs.bizfirstai.com), or join the
discussion at [community.bizfirstai.com](https://community.bizfirstai.com).

## License

Community node maintained by the [BizFirst.Ai](https://bizfirstai.com) team.
