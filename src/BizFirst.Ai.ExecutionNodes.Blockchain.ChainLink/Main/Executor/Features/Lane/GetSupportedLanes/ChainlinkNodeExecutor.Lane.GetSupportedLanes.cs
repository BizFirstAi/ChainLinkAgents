// Code review guidelines: 020_NodeServerProject-Engineer/Guidelines/14_node-executor-integration-code/guideline.md
using static BizFirst.Ai.ProcessEngine.Service.Constants.ExecutionConstants;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

//IMPORTANT: "code-step" comments must not be changed. This is a coding checklist used as a template.
public sealed partial class ChainlinkNodeExecutor
{
    private async Task<NodeExecutionResult> _Chainlink_Lane_GetSupportedLanesAsync(
        NodeExecutionContext nodeExecutionContext,
        CancellationToken cancellationToken = default)
    {
        //code-step: 1.1 - Validate settings exist and cast to LaneGetSupportedLanesInfo
        if (mySettings?.ActiveInfo is not LaneGetSupportedLanesInfo info)
            return SimpleErrorOperationUnfound();

        //code-step: 1.2 - Create result manager for output handling
        var resultManager = NodeResultOperateManager.CreateInstance(nodeExecutionContext);

        var credential = await _ResolveCredentialAsync(cancellationToken);

        try
        {
            //code-step: 1.3 - Call Chainlink lane service to list supported lanes
            var r = await _laneService.GetSupportedLanesAsync(info.SourceNetworkName, credential, cancellationToken);
            if (!r.Success)
                return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, r.ErrorMessage, this);

            //code-step: 1.4 - Report progress milestone to execution context
            await ReportNodeProgress_ResourceOperation(nodeExecutionContext, "IntegrationCallCompleted");

            //code-step: 1.5 - Extract supported-lanes list from result
            var lanes = r.Lanes.Select(l => new Dictionary<string, object>
            {
                { "sourceNetworkName", l.SourceNetworkName },
                { "sourceChainSelector", l.SourceChainSelector },
                { "destinationNetworkName", l.DestinationNetworkName },
                { "destinationChainSelector", l.DestinationChainSelector },
            }).ToList();

            //code-step: 1.6 - Build output metadata dictionary
            var outputData = new Dictionary<string, object>
            {
                { "status", "success" },
                { "resource", mySettings?.Resource ?? string.Empty },
                { "operation", mySettings?.Operation ?? string.Empty },
                { "count", lanes.Count },
            };

            //code-step: 1.7 - Convert supported-lanes list to standard items array
            outputData[OutputFieldNameConstants.CONST_items] = WrapJsonIntoItems(lanes, nodeExecutionContext);

            //code-step: 1.8 - Write output (handles TargetDataPath writes + items downstream)
            return await WriteOutputData(ExecutionConstants.OutputPorts.Success, outputData, lanes, nodeExecutionContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            //code-step: 1.9 - Catch exceptions and return error with context
            return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, $"lane/getSupportedLanes failed: {ex.Message}", this);
        }
    }
}
