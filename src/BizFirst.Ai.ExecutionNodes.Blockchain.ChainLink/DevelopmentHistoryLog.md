# Development History Log

## 2026-07-22 — Initial Implementation
- Sprint: ExecutionNodes / Chainlink
- Status: All 3 projects (Domain, Services, ExecutionNode) implemented and building clean; 11
  operations across 3 resources (Message x4, Router x4, Lane x3) fully routed; Operation DTO/Factory
  layer; DI registration wired into the host composition root
  (`ServiceCollectionExtensionsForAI.cs`'s `Plugins_RegisterAllNodes()`) and the host project's
  `ProjectReference`; 30 unit tests passing (ABI-encoding correctness, rate-limit retry behavior,
  Domain result-record contract, static reference-table lookups).
- Reference: `Documentation/Employees/flow-studio/node-engineer/010_NodeDesign-Engineer/ExecutionNodes/chainlink/44_Features/Design/00_INDEX.md` (v1.4, after four review passes)
- Node Type Code: `chainlink`
- Two operations from the original design (`CCIP-MSG03`/`CCIP-MSG04`) were cut — no real CCIP API
  endpoint backs either, per the design's own §17 open question 2 recommendation.
- Credential resolution simplified from the design's sketched `IChainlinkCredentialResolver` service
  class to a direct `ReadCredentialRawPrimaryAsync` + `ApiKeyRecord` read on the executor, matching
  every real sampled node's actual pattern (Slack/SES/SqlServer/Binance) rather than introducing a
  service the codebase doesn't otherwise use for this shape of credential.
- `router/isChainSupported`/`router/getFee` implemented as real on-chain reads via a small, independent
  Nethereum client (`ChainlinkOnChainReader`) rather than only a prepared call descriptor — resolves an
  ambiguity in the original design (§15) in favor of the design's own §14.4 output contract, which
  promises a real resolved value. Confirmed via a standalone compile-and-run check during
  implementation (not assumed) that Nethereum's ABI encoder correctly handles the nested
  `EVM2AnyMessage` tuple, including the `tuple[]` `tokenAmounts` array.
- `router`/`convertChainSelector`/`getAddress`/`isChainSupported`/`getFee` take a specific chain name
  (e.g. `"ethereum-mainnet"`) rather than the design's original `network: "mainnet"|"testnet"` binary
  key — resolves the design's own §17 open question 4 (that binary key was flagged as insufficient for
  a genuinely multi-chain resource).
- Added a `gasLimit` config key for `message/build`/`router/getFee`, not present in the original
  design's §13.2 config-key table — required by both `EVMExtraArgsV1`/`GenericExtraArgsV2`.
- CCIP API responses hand-parsed via `JsonDocument`/`JsonElement` (matching the confirmed, established
  codebase convention — no project in `AI/ExecutionNodes` defines a custom `JsonConverter`), not
  deserialized into POCOs as an earlier design-review draft had sketched. Field lookups try several
  candidate names per value since the real API's exact casing was never fully reconfirmed.

## 2026-07-22 — Critical Code Review

- Reviewed the just-written implementation for production-readiness. Found and fixed real bugs:
  - Nethereum's ABI encoder does not reliably validate EVM addresses — confirmed via a standalone
    compiled check that it silently accepts a too-short address (`"0x123"`) with no error, and throws
    `AbiEncodingException` (not `ArgumentException`) for non-hex input. Added explicit `0x` + 40-hex
    validation in `ChainlinkMessageBuilder` before any value reaches Nethereum.
  - `ChainlinkRouterService.GetFeeAsync` had no `catch (ArgumentException)` before its generic catch,
    so a bad address/extraArgsVersion was mis-reported as `CCIP_API_UPSTREAM_ERROR`. Fixed in both
    `GetFeeAsync` and `ChainlinkMessageService.Build`, now mapping by `ex.ParamName` to distinct codes
    (`INVALID_ADDRESS`, `INVALID_TOKEN_AMOUNT`, `INVALID_GAS_LIMIT`).
  - `CancellationToken` was accepted by `ChainlinkOnChainReader` but never honored — confirmed via
    reflection that Nethereum 6.0.0's `Function.CallAsync` has no cancellation overload at all. Added
    an early `ThrowIfCancellationRequested()` and replaced the raw unmanaged `new Web3(url)` with an
    `IHttpClientFactory`-backed RPC client (`Chainlink.Rpc`, 30s timeout) — the real mechanism now
    providing resilience against a hanging RPC endpoint.
  - Duplicate `"destNetworkInfo"` candidate name in `ChainlinkMessageService.ReadNetworkInfo`; `chainId`
    read as string-only when the real API might return it as a number — both hardened.
  - `ChainlinkConfigParsingHelpers.ParseTokenAmounts`'s doc comment claimed malformed entries are
    "skipped rather than throwing," but `JsonNode.GetValue<string>()` actually throws for a non-string
    JSON value (e.g. a bare number). Fixed to genuinely never throw.
  - `ChainlinkCcipApiClient`: wrapped the success-path `JsonDocument.Parse` in try/catch (a non-JSON
    2xx body is a real failure mode), added an `Accept: application/json` header.
  - Added regression tests for every fix (30 → 43 tests).

## 2026-07-22 — Cross-File Consistency Pass

- Explicitly checked resource/operation string literals, config keys, error codes, and output field
  names across every layer (Factory ↔ routing switch ↔ feature partials ↔ Domain records) rather than
  reviewing files in isolation. Found and fixed real behavioral bugs invisible to a single-file review:
  - `router/getFee`'s output re-derived `feeToken` from the raw config input (`info.FeeToken`) instead
    of the value actually used for the on-chain fee calculation — an empty/null config value produced
    an empty output even when the real calculation used the native-gas-token zero address. Added
    `FeeToken` to `ChainlinkRouterGetFeeResult`, threaded the resolved value
    (`parts.FeeTokenAddress`) through from `ChainlinkRouterService.GetFeeAsync`, and had the feature
    partial read it directly instead of re-deriving a different value.
  - `message/getTokenTransferRateLimit`'s feature partial only surfaced `RateLimitCurrent` under the
    ambiguous name `"tokenTransferRateLimit"`, silently dropping `RateLimitCapacity` entirely — both
    Domain record fields are now surfaced under names matching their own property names.
  - `lane/getSupportedLanes`'s config key was `"sourceNetworkName"` while every other operation
    identifying a single network (`router/getAddress`, `isChainSupported`, `getFee`,
    `convertChainSelector`) used `"network"`, with no accompanying "destination" key on the same
    operation to justify the divergence — standardized to `"network"` (C# property name unchanged,
    since it correctly matches `ChainlinkLane.SourceNetworkName` and the per-lane output shape, which
    legitimately does need source/destination disambiguation).
  - `ChainlinkLaneGetLatencyResult.Ok` accepted `double?` even though a Success result can never
    actually carry a null latency (`ChainlinkLaneService.GetLatencyAsync` already returns `Fail()`
    before ever calling `Ok()`) — this forced the feature partial into an ambiguous `?? 0` fallback
    that could not be distinguished from a genuine zero-latency value. Changed to a non-nullable
    `double` parameter, removing the dead fallback entirely.
  - Added regression tests for the `feeToken` fix (43 → 44 tests).

## 2026-07-23 — Node Templates (Flow Studio Library)

- Created 11 `Template_DataTemplates` SQL files (one per operation, matching the real Binance
  ground-truth convention: `code` = base NodeTypeName `"chainlink"`, `design-class` =
  operation-specific, files under `BizFirstFiDB/.../dbo/Data/projects/Blockchain/Chainlink/DataTemplates/`)
  covering all 11 routed (resource, operation) pairs exactly as they appear in
  `ChainlinkNodeExecutor.cs`'s `_ExecuteInternal_Route_Async` switch — no extras, no gaps.
- Initial ID assignment (20000512-20000522) was based on an incomplete "next available ID" scan that
  missed a `Blockchain/Solana/DataTemplates/` folder occupying 20000512-20000528. Caught before DB
  sync via a full DB-wide collision check. Renumbered all 11 files to 20000529-20000539 (true global
  max at the time was 20000528). Re-verified: no ID collisions across all 918 other DataTemplateIDs in
  the DB, no duplicate IDs within the new set, every file's JSON payload parses, and every
  resource/operation pair matches the C# routing switch 1:1.
- Fixed three literal apostrophes in generated descriptions ("token's", "network's", "Network's")
  that would have broken the T-SQL `N'...'` string literal — reworded to avoid the apostrophe rather
  than attempting in-string escaping, and baked the fix into the generator script itself (not just the
  output files) so any future regeneration stays correct.

## 2026-07-23 — Node Full Validation (080_Validate, 14 rules)

- Ran all 14 `080_Validate` rules against the complete node (C#, DataTemplates, ProcessElementType,
  Forms). Full report: `080_Validate/NodeReports/Chainlink-NodeReport.md`.
- **Real, live FormID collision caught and fixed**: a fresh DB/disk-wide ID scan (not trusting the
  earlier "next available ID" assignment) found that Bitcoin (34 forms) and Coinbase (40 forms) were
  both added to the repo after Chainlink's 11 Atlas Forms were first created, directly colliding with
  10 of 11 IDs (`10000476-10000486`). Renumbered all 11 forms to `10000551-10000561` (true next-available
  after Coinbase's max `10000550`), updated `ChainlinkOverview.md` to match, re-verified zero collisions
  against the full DB tree. `Template_DataTemplates`/`Process_ProcessElementTypes` had no new collisions.
- **Rule 011-Q fixed**: all 11 feature partials hardcoded `{"resource","message"}, {"operation","build"}`
  -style literals instead of `mySettings?.Resource`/`mySettings?.Operation` — fixed across all 11 files.
- **Rule 011-M fixed**: `BaseUrl` (should be `BaseURL`) in `ChainlinkApiClientOptions.cs` and 2 usage
  sites — matches the real Binance/Ethereum sibling pattern exactly but fixed anyway as a low-risk,
  single-property rename (Binance/Ethereum's own copies left untouched; not referenced anywhere as an
  exact config-key string, so the rename is safe for .NET's case-insensitive options binding).
- **Rule 014 fixed — pervasive, the largest fix of this pass**: `MessageId`/`ChainId`/`NativeChainId`/
  `TryGetByNativeChainId` (properties/method) and `messageId`/`nativeChainId`/`chainId`/
  `resolvedMessageId` (parameters/locals) all needed renaming to their `ID`-suffixed form across ~15
  files spanning Domain, Services, and the ExecutionNode project. Each fix was verified to leave the
  corresponding camelCase **string literal** (`"messageId"`, `"nativeChainId"`, etc. — governed by Rule
  013, must stay lowercase-first) untouched, and to leave .NET structured-logging template placeholders
  (`{MessageId}`) and XML-doc/error-message prose describing external API field names alone (not C#
  identifiers, out of Rule 014's scope). Two test files needed matching updates.
  Verified via full rebuild of all 3 projects + Tests (0 errors) and full test run: 44/44 pass.
- **Rule 011-R and Rule 012 confirmed as known, pre-existing, non-Chainlink-specific gaps** — both
  reproduce identically in the real, shipped Binance/Ethereum/Solana siblings this node was modeled on
  (missing output-items merge API is a platform-wide gap across 6 Blockchain-domain nodes; missing
  domain-constants class matches the whole Blockchain-domain's real architecture, not the
  Slack/MongoDB-era pattern Rule 012 describes). Flagged, not fixed, matching the exact precedent the
  Solana validation pass set for the same two findings.
- Database sync (Rules 002/003 DB-record checks) intentionally left undone in this validation pass —
  no SQL executed against the live DB yet, pending explicit user confirmation.

## 2026-07-23 — Database Sync (090_Synchup)

- Synced all 23 SQL files (11 `Template_DataTemplates`, 1 `Process_ProcessElementTypes`, 11
  `Atlas_Forms`) to the live local DB (`localhost/BizFirstAtlasDB`) following the `090_Synchup`
  process: connection confirmed from `appsettings.Development.json`, pre-flight checks passed (no
  duplicate IDs, all files use the `DECLARE`-based JSON pattern), a fresh query classified all 23
  records as New, executed via `sqlcmd` in the required order (DataTemplates → ProcessElementTypes →
  Forms), each reporting `(1 row affected)` with no errors.
- Post-sync verification: `Process_ProcessElementTypes` (`Code = 'chainlink'`) 0→1,
  `Template_DataTemplates` (range `20000529-20000539`) 0→11, `Atlas_Forms` (range
  `10000551-10000561`) 0→11. All 11 DataTemplates independently re-verified as
  `JSON_VALUE(ContentData, '$.code') = 'chainlink'`, no duplicates. Overall DB totals moved by exactly
  the expected amounts.
- Rules 002/003 of `080_Validate/NodeReports/Chainlink-NodeReport.md` now fully pass. Node is fully
  synced; API host restart still required to clear Flow Studio's 24-hour template cache before the
  node appears live (deployment-environment action, out of session scope).
