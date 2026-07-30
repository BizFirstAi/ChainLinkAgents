# Chainlink ExecutionNode — Operation Reference

`NodeTypeName`: `chainlink` · Area: `Blockchain` · Protocol: CCIP (Cross-Chain Interoperability Protocol)

Every operation takes `resource` and `operation` as config keys, plus the operation-specific fields listed
below. No wallet, no signing key, and no third-party SDK is required for any operation — `message/build` is
pure local ABI encoding, the HTTP operations call the free, public CCIP API v2
(`api.ccip.chain.link/v2`), and `router/isChainSupported`/`router/getFee` make a real, independent on-chain
read (needs an RPC URL — see [Configuration](guide/01-configuration.html)).

## message

| operation | fields | notes |
|---|---|---|
| `build` | `receiverAddress`*, `payloadData`, `tokenAmounts`, `feeToken`, `extraArgsVersion` (def `genericV2`), `gasLimit`, `allowOutOfOrderExecution` | Local, pure ABI encoding — no HTTP, no RPC. `estimatedMessageId` is always empty. |
| `getStatus` | `messageId`* | HTTP `GET /messages/{messageId}`. Returns `executionState` (`UNTOUCHED`/`IN_PROGRESS`/`SUCCESS`/`FAILURE`) + source/dest network info. |
| `checkManualExecutionRequired` | `messageId`* | Derived from `getStatus` (`true` iff `executionState == FAILURE`). Reports only — does not submit re-execution. |
| `getTokenTransferRateLimit` | `tokenAddress`*, `destinationChainSelector`* | Returns a prepared call descriptor, not a resolved value — no confirmed on-chain rate-limiter ABI. |

## router

| operation | fields | notes |
|---|---|---|
| `getAddress` | `network` (def `ethereum-mainnet`) | Static per-network reference table lookup. |
| `isChainSupported` | `network`, `destinationChainSelector`* | Real on-chain read via `IRouterClient.isChainSupported`. Requires `Chainlink:NetworkRpcUrls:{network}`. |
| `getFee` | `network`, `destinationChainSelector`*, `receiverAddress`*, `payloadData`, `tokenAmounts`, `feeToken`, `extraArgsVersion`, `gasLimit`, `allowOutOfOrderExecution` | Real on-chain read via `IRouterClient.getFee`, same message shape as `message/build`. Requires an RPC URL. |
| `convertChainSelector` | exactly one of `network` \| `nativeChainId` \| `chainSelector` | Static lookup table conversion between the three identifier types. |

## lane

| operation | fields | notes |
|---|---|---|
| `getSupportedLanes` | `network` (optional) | HTTP `GET /chains`. Lists supported (source, destination) lane pairs. |
| `getLatency` | `sourceChainSelector`*, `destinationChainSelector`* | HTTP `GET /lanes`. Returns `latencyEstimateSeconds`. |
| `getRiskManagementStatus` | `chainSelector` (optional, omit for global) | HTTP `GET /verifiers`. Returns a status string, e.g. `NORMAL`. |

`*` = required.

### Supported networks (bundled reference table)

- `ethereum-mainnet` — chain ID `1`, chain selector `5009297550715157269` (independently confirmed)
- `arbitrum-mainnet` — chain ID `42161`, chain selector `4949039107694359620` (placeholder — unconfirmed)
- `base-mainnet` — chain ID `8453`, chain selector `15971525489660198786` (placeholder — unconfirmed)

Router contract addresses for all three are representative placeholders pending a live CCIP Directory pull —
see [Networks](guide/02-networks.html).

## Scope

This node covers **CCIP only**. Chainlink Data Feeds are out of scope (delegated to the Ethereum
ExecutionNode's generic contract-call path + Standards Registry). VRF, Automation, and Functions are not
implemented by any node in this codebase. Two originally-designed message operations (get message by
transaction, list messages by address) were cut — no real CCIP API endpoint backs either. See
[Roadmap](guide/10-roadmap.html).

## Full Guide

See [`guide/`](guide/) for the full 11-page UserGuide: configuration, networks, per-resource operation
references, CCIP concepts (EVM2AnyMessage, extraArgs versions, fee tokens, chain selectors), input/output,
examples, troubleshooting, and roadmap.
