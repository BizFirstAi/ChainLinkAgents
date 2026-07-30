# BizFirst.Integration.Chainlink.Domain

Pure C# result records and shared value types for the Chainlink CCIP integration — zero logic, zero I/O.

- `Common/` — `ChainlinkNetworkInfo` (source/dest chain identity, string-typed chain selector),
  `ChainlinkTokenAmount` (string-typed uint256 amount), `ChainlinkLane`, `ChainlinkNetworkReference`
  (static per-network reference table row).
- `Results/{Message,Router,Lane}/` — one `Ok()`/`Fail()` result record per operation.

Chain selectors and token amounts are strings, not numeric types, throughout this project — CCIP chain
selectors (e.g. Ethereum mainnet's `5009297550715157269`) exceed JavaScript's safe-integer boundary
(2^53-1) by roughly 500x, and the real CCIP API itself returns them as JSON strings for exactly that
reason.

See the executor project's README for the full design rationale and design-doc reference.
