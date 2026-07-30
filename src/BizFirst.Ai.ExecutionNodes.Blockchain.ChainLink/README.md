# BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink

Chainlink ExecutionNode — CCIP (Cross-Chain Interoperability Protocol) message building, status
tracking, Router reads, and Lane/RMN health, against the real CCIP API v2 and a minimal, independent
on-chain read path.

## Node Type Code

`chainlink`

## Resources & Operations (11 total)

| Resource | Operation | Mechanism |
|---|---|---|
| message | build | Local, pure ABI encoding (Nethereum) — no HTTP, no RPC |
| message | getStatus | HTTP `GET messages/{messageId}` (api.ccip.chain.link/v2) |
| message | checkManualExecutionRequired | Derived from getStatus (`executionState == FAILURE`) |
| message | getTokenTransferRateLimit | Prepared call descriptor — no confirmed on-chain ABI (see Open Questions below) |
| router | getAddress | Static per-network reference table lookup |
| router | isChainSupported | Real on-chain read (`IRouterClient.isChainSupported`) via a minimal, independent Nethereum client |
| router | getFee | Real on-chain read (`IRouterClient.getFee`), builds the same EVM2AnyMessage tuple as message/build |
| router | convertChainSelector | Static lookup table (native chain ID ⇄ CCIP chain selector) |
| lane | getSupportedLanes | HTTP `GET chains` |
| lane | getLatency | HTTP `GET lanes` |
| lane | getRiskManagementStatus | HTTP `GET verifiers` |

Two operations from the original design (`CCIP-MSG03` Get Message by Transaction, `CCIP-MSG04` List
Messages by Address) were **cut from this implementation** — a deep design review found no real CCIP
API endpoint backs either (the design's own §17 open question 2 recommended this).

## Design Source

`Documentation/Employees/flow-studio/node-engineer/010_NodeDesign-Engineer/ExecutionNodes/chainlink/44_Features/Design/00_INDEX.md`
(v1.4, after four review passes — architecture conformance, operation-level API verification, a full
Guidelines audit, and a final self-consistency/runtime-correctness pass).

## Architecture

Three-project split: this executor project + `BizFirst.Integration.Chainlink.Domain` (result records,
common value types) + `BizFirst.Integration.Chainlink.Services` (HTTP client, ABI encoding, on-chain
reads, the 3 resource services).

- `Main/Executor/ChainlinkNodeExecutor.cs` — routing (`(resource, operation)` switch), constructor.
- `Main/Executor/ChainlinkNodeExecutor.Config.cs` — settings factory, output-port init, `GetNodeExecutorManifest()`.
- `Main/Executor/ChainlinkNodeExecutor.Credentials.cs` — resolves the optional CCIP API key via `ReadCredentialRawPrimaryAsync` + `ApiKeyRecord`.
- `Main/Executor/Features/{Resource}/{Operation}/` — one partial file per operation, 9-step body per Guideline 14.
- `Main/OperationInfo/` — `BaseChainlinkOperationInfo` + `ChainlinkOperationInfoFactory` + 11 per-operation config DTOs.
- `Support/ChainlinkDependency.cs` — DI registration. **Must also be wired into `ServiceCollectionExtensionsForAI.cs`'s `Plugins_RegisterAllNodes()`** — already done as part of this implementation, alongside the host project's `ProjectReference` — see the class-level doc comment on `ChainlinkDependency` for why both steps are required.

## Real Deviations From the Original Design Doc

Implementing against real, currently-shipping BizFirst source (Slack/SES/SqlServer/Binance) rather
than the design doc's own sketches surfaced several corrections, made deliberately during this
implementation pass — not oversights:

1. **No `IChainlinkCredentialResolver` service class.** The design's §8.2 sketched a bespoke
   credential-resolver service, constructor-injected with the framework's `ICredentialResolver`. No
   real BizFirst node does this. Every sampled real executor resolves credentials directly on the
   Scoped executor and passes the resolved value down as a plain per-call parameter — this avoids the
   DI captive-dependency risk a bespoke resolver could introduce if ever injected into a Singleton.
2. **`router/getAddress`/`isChainSupported`/`getFee`/`convertChainSelector` take a specific chain
   name** (e.g. `"ethereum-mainnet"`), not the design's original `network: "mainnet"|"testnet"` binary
   key — the design's own §17 open question 4 flagged that binary key as insufficient for a
   genuinely multi-chain resource; this resolves it concretely.
3. **`isChainSupported`/`getFee` perform a real, independent on-chain read** (via a minimal Nethereum
   client in `ChainlinkOnChainReader`, with a hand-written `IRouterClient` ABI fragment — confirmed
   against 00_INDEX.md §4.2's real Solidity interface), rather than only preparing a call descriptor
   for a separate Ethereum ExecutionNode step. This was a genuine ambiguity in the design (§15 said
   both "delegates internally" and "no C# reference to Ethereum's project" in different places) —
   resolved in favor of actually fulfilling the design's own §14.4 output contract (a real
   `isChainSupported`/`feeAmount` value), while still taking zero `ProjectReference` to the Ethereum
   ExecutionNode's own C# code.
4. **`message/getTokenTransferRateLimit` returns a prepared call descriptor, not a resolved value** —
   unlike isChainSupported/getFee, no real TokenPool rate-limiter ABI was ever confirmed (design §17),
   so this operation does not guess one.
5. **CCIP API responses are hand-parsed via `JsonDocument`/`JsonElement`, not deserialized into POCOs**
   — matching the established, confirmed convention across this codebase (no project in
   `AI/ExecutionNodes` defines a custom `JsonConverter`; Binance's own `BinanceApiClient` documents the
   same choice). Field-name lookups try several candidate names per value
   (`ChainlinkCcipApiClient.TryGetAny`) since the design review could not fully settle the real API's
   exact field casing (§17 open question 13).
6. Requires a `gasLimit` config key for `message/build`/`router/getFee` that the original design's
   §13.2 config-key table never defined — the `EVMExtraArgsV1`/`GenericExtraArgsV2` extraArgs structs
   both require it; defaults to Chainlink's own documented `200_000` when omitted.

## Configuration

Bound from the `"Chainlink"` configuration section:

```json
{
  "Chainlink": {
    "BaseUrl": "https://api.ccip.chain.link/v2/",
    "NetworkRpcUrls": {
      "ethereum-mainnet": "https://your-rpc-provider/...")
    }
  }
}
```

`NetworkRpcUrls` is required only for `router/isChainSupported` and `router/getFee` (the two
operations that make a real on-chain read) — every other operation works with no RPC configuration at
all. No production default RPC URL is bundled (unlike the CCIP API's own stable public base URL) —
an RPC endpoint is deployment-specific infrastructure that must never be hardcoded as a source default.

## Open Questions Carried Into Implementation

See the design doc's §17 for the full list (13 items after the final review pass). Most load-bearing
for anyone extending this node:
- The exact CCIP API v2 response schema (field-name casing) was never fully reconfirmed — a re-fetch
  during design review got a contradictory result from the same live, JS-rendered docs page. Reconfirm
  with direct browser/Postman access before relying on this node against production traffic.
- The `ChainlinkNetworkReferenceTable`'s Router addresses beyond Ethereum mainnet are representative
  placeholders, not independently confirmed against the live CCIP Directory.
- `message/getTokenTransferRateLimit`'s exact on-chain getter name is unconfirmed.

## Post-Implementation Review Rounds

Two additional passes ran after the initial implementation, both against the real, already-committed
code (see `DevelopmentHistoryLog.md` for the full, dated list):

1. **Critical code review** — found and fixed real bugs, not style nits: Nethereum's ABI encoder
   silently accepts a malformed/too-short EVM address with no error (now validated explicitly before
   any value reaches it); a missing `catch (ArgumentException)` mis-reported validation failures as
   upstream API errors; `CancellationToken` was accepted but never actually honored by Nethereum's
   `Function.CallAsync` (now backed by a bounded-timeout `IHttpClientFactory` client instead); a
   duplicate candidate field name; config parsing that could throw on a non-string JSON value despite
   its own doc comment claiming otherwise.
2. **Cross-file consistency pass** — found real behavioral bugs surfaced only by comparing files
   against each other: `router/getFee`'s output re-derived `feeToken` from raw config instead of the
   value actually used for the on-chain calculation (so an empty config value showed as empty output
   even when the real native-gas-token zero address was used); `message/getTokenTransferRateLimit`
   silently dropped half of its own Domain result record's fields; a config key
   (`sourceNetworkName`) diverged from the `network` key used by every other operation that identifies
   a single network with no accompanying reason; a nullable-on-guaranteed-success type let a real
   zero-latency value become indistinguishable from "no data."

Test count grew from an initial 30 to 44 as regression coverage was added for each fix.
