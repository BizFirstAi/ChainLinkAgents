namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>Per-integration marker type over the framework's BaseNodeOperationInfo — mirrors the real BaseBinanceOperationInfo pattern (no shared fields across Chainlink's 11 operations beyond what the framework base already provides; the optional CCIP API key credential is read once on the executor, not per-operation).</summary>
internal abstract class BaseChainlinkOperationInfo : BaseNodeOperationInfo
{
}
