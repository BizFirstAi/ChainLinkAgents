// Code review guidelines: 020_NodeServerProject-Engineer/Guidelines/14_node-executor-integration-code/guideline.md
using static BizFirst.Ai.ProcessEngine.Service.Constants.ExecutionConstants;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

//IMPORTANT: "code-step" comments must not be changed. This is a coding checklist used as a template.
public sealed partial class ChainlinkNodeExecutor
{
    private async Task<NodeExecutionResult> _Chainlink_Message_CheckManualExecutionRequiredAsync(
        NodeExecutionContext nodeExecutionContext,
        CancellationToken cancellationToken = default)
    {
        //code-step: 1.1 - Validate settings exist and cast to MessageCheckManualExecutionRequiredInfo
        if (mySettings?.ActiveInfo is not MessageCheckManualExecutionRequiredInfo info)
            return SimpleErrorOperationUnfound();

        //code-step: 1.2 - Create result manager for output handling
        var resultManager = NodeResultOperateManager.CreateInstance(nodeExecutionContext);

        var credential = await _ResolveCredentialAsync(cancellationToken);

        try
        {
            //code-step: 1.3 - Call Chainlink message service to check manual execution eligibility (derived from getStatus)
            var r = await _messageService.CheckManualExecutionRequiredAsync(info.MessageID, credential, cancellationToken);
            if (!r.Success)
                return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, r.ErrorMessage, this);

            //code-step: 1.4 - Report progress milestone to execution context
            await ReportNodeProgress_ResourceOperation(nodeExecutionContext, "IntegrationCallCompleted");

            //code-step: 1.5 - Extract manual-execution-eligibility record from result
            var data = new Dictionary<string, object>
            {
                { "manualExecutionRequired", r.ManualExecutionRequired },
                { "executionState", r.ExecutionState ?? string.Empty },
            };

            //code-step: 1.6 - Build output metadata dictionary
            var outputData = new Dictionary<string, object>
            {
                { "status", "success" },
                { "resource", mySettings?.Resource ?? string.Empty },
                { "operation", mySettings?.Operation ?? string.Empty },
            };

            //code-step: 1.7 - Convert eligibility record to standard items array
            outputData[OutputFieldNameConstants.CONST_items] = WrapJsonIntoItems(data, nodeExecutionContext);

            //code-step: 1.8 - Write output (handles TargetDataPath writes + items downstream)
            return await WriteOutputData(ExecutionConstants.OutputPorts.Success, outputData, data, nodeExecutionContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            //code-step: 1.9 - Catch exceptions and return error with context
            return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, $"message/checkManualExecutionRequired failed: {ex.Message}", this);
        }
    }
}
