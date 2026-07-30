// Code review guidelines: 020_NodeServerProject-Engineer/Guidelines/14_node-executor-integration-code/guideline.md
using static BizFirst.Ai.ProcessEngine.Service.Constants.ExecutionConstants;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

//IMPORTANT: "code-step" comments must not be changed. This is a coding checklist used as a template.
public sealed partial class ChainlinkNodeExecutor
{
    private async Task<NodeExecutionResult> _Chainlink_Router_GetFeeAsync(
        NodeExecutionContext nodeExecutionContext,
        CancellationToken cancellationToken = default)
    {
        //code-step: 1.1 - Validate settings exist and cast to RouterGetFeeInfo
        if (mySettings?.ActiveInfo is not RouterGetFeeInfo info)
            return SimpleErrorOperationUnfound();

        //code-step: 1.2 - Create result manager for output handling
        var resultManager = NodeResultOperateManager.CreateInstance(nodeExecutionContext);

        try
        {
            //code-step: 1.3 - Call Chainlink router service to build the message and read getFee on-chain
            var r = await _routerService.GetFeeAsync(
                info.NetworkName, info.DestinationChainSelector, info.ReceiverAddress, info.PayloadData,
                info.TokenAmounts, info.FeeToken, info.ExtraArgsVersion, info.GasLimit, info.AllowOutOfOrderExecution,
                cancellationToken);
            if (!r.Success)
                return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, r.ErrorMessage, this);

            //code-step: 1.4 - Report progress milestone to execution context
            await ReportNodeProgress_ResourceOperation(nodeExecutionContext, "IntegrationCallCompleted");

            //code-step: 1.5 - Extract fee record from result
            // Consistency fix: "feeToken" must reflect the RESOLVED address actually used for the fee
            // calculation (r.FeeToken, the native-gas-token zero address when info.FeeToken was
            // null/empty), not be re-derived from the raw config input — the earlier version of this
            // partial reported an empty string here even when the fee was actually computed using the
            // zero address, which would have misled a workflow author reading this output.
            var data = new Dictionary<string, object>
            {
                { "feeAmount", r.FeeAmount ?? string.Empty },
                { "feeToken", r.FeeToken ?? string.Empty },
            };

            //code-step: 1.6 - Build output metadata dictionary
            var outputData = new Dictionary<string, object>
            {
                { "status", "success" },
                { "resource", mySettings?.Resource ?? string.Empty },
                { "operation", mySettings?.Operation ?? string.Empty },
            };

            //code-step: 1.7 - Convert fee record to standard items array
            outputData[OutputFieldNameConstants.CONST_items] = WrapJsonIntoItems(data, nodeExecutionContext);

            //code-step: 1.8 - Write output (handles TargetDataPath writes + items downstream)
            return await WriteOutputData(ExecutionConstants.OutputPorts.Success, outputData, data, nodeExecutionContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            //code-step: 1.9 - Catch exceptions and return error with context
            return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, $"router/getFee failed: {ex.Message}", this);
        }
    }
}
