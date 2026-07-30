# BizFirst.Integration.Chainlink.Services

CCIP API v2 HTTP client, EVM2AnyMessage/extraArgs ABI encoder, minimal independent on-chain reader,
and the 3 resource services (Message, Router, Lane).

- `Http/ChainlinkCcipApiClient.cs` — the CCIP API v2 REST client. Hand-parses responses via
  `JsonDocument`/`JsonElement` (matching the established codebase convention), trying several
  candidate field names per value since the real API's exact casing was never fully reconfirmed
  during design review.
- `Http/ChainlinkRateLimitHandler.cs` — retries 429/502/503/504, honors `Retry-After`.
- `Encoding/ChainlinkMessageBuilder.cs` — pure, dependency-free ABI encoder for the
  `EVM2AnyMessage` tuple and tag-prefixed `extraArgs` bytes. Uses `Nethereum.ABI` (already a
  repo-wide dependency, same package the Ethereum ExecutionNode uses) — verified against Chainlink's
  own two published fixed 4-byte tag selectors (`EVMExtraArgsV1` = `0x97a657c9`, `GenericExtraArgsV2` =
  `0x181dcf10`).
- `OnChain/ChainlinkOnChainReader.cs` — minimal, independent Nethereum-based reader for
  `router/isChainSupported`/`router/getFee`. No `ProjectReference` to the Ethereum ExecutionNode's
  Services project anywhere in this project.
- `Common/ChainlinkNetworkReferenceTable.cs` — static per-network Router address / chain-selector
  table. Only Ethereum mainnet's chain selector is independently confirmed; every other entry is a
  representative placeholder pending a live CCIP Directory pull.

See the executor project's README for the full design rationale and design-doc reference.
