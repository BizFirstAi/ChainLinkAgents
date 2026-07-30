namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>Typed settings for <see cref="ChainlinkNodeExecutor"/> — resolves the active operation DTO from (resource, operation).</summary>
internal sealed class ChainlinkNodeExecutorSettings : ResourceBasedNodeExecutorSettings
{
    public override BaseNodeOperationInfo? CreateActiveInfo() =>
        ChainlinkOperationInfoFactory.Create(Resource, Operation, ConfigReader);
}
