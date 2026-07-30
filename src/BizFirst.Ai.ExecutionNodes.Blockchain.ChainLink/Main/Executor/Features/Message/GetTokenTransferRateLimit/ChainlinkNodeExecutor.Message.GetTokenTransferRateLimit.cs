// Code review guidelines: 020_NodeServerProject-Engineer/Guidelines/14_node-executor-integration-code/guideline.md
using static BizFirst.Ai.ProcessEngine.Service.Constants.ExecutionConstants;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

//IMPORTANT: "code-step" comments must not be changed. This is a coding checklist used as a template.
public sealed partial class ChainlinkNodeExecutor
{
    private async Task<NodeExecutionResult> _Chainlink_Message_GetTokenTransferRateLimitAsync(
        NodeExecutionContext nodeExecutionContext,
        CancellationToken cancellationToken = default)
    {
        //code-step: 1.1 - Validate settings exist and cast to MessageGetTokenTransferRateLimitInfo
        if (mySettings?.ActiveInfo is not MessageGetTokenTransferRateLimitInfo info)
            return SimpleErrorOperationUnfound();

        //code-step: 1.2 - Create result manager for output handling
        var resultManager = NodeResultOperateManager.CreateInstance(nodeExecutionContext);

        try
        {
            //code-step: 1.3 - Call Chainlink message service to prepare the rate-limit call descriptor (no confirmed on-chain ABI — see 00_INDEX.md §17)
            var r = await _messageService.GetTokenTransferRateLimitAsync(info.TokenAddress, info.DestinationChainSelector, cancellationToken);
            if (!r.Success)
                return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, r.ErrorMessage, this);

            //code-step: 1.4 - Report progress milestone to execution context
            await ReportNodeProgress_ResourceOperation(nodeExecutionContext, "IntegrationCallCompleted");

            //code-step: 1.5 - Extract rate-limit descriptor record from result
            // Consistency fix: the Domain result carries BOTH RateLimitCapacity and RateLimitCurrent
            // (both currently always null — no confirmed on-chain ABI, see r's own doc comment), but
            // this partial previously surfaced only RateLimitCurrent under the ambiguous name
            // "tokenTransferRateLimit", silently dropping RateLimitCapacity. Both are now surfaced
            // under names that match the Domain record's own property names (camelCased), consistent
            // with how every other feature partial maps its result record 1:1 to output fields.
            var data = new Dictionary<string, object>
            {
                { "contractAddress", r.ContractAddress ?? string.Empty },
                { "functionName", r.FunctionName ?? string.Empty },
                { "functionArgsJson", r.FunctionArgsJson ?? string.Empty },
                { "rateLimitCapacity", r.RateLimitCapacity ?? string.Empty },
                { "rateLimitCurrent", r.RateLimitCurrent ?? string.Empty },
            };

            //code-step: 1.6 - Build output metadata dictionary
            var outputData = new Dictionary<string, object>
            {
                { "status", "success" },
                { "resource", mySettings?.Resource ?? string.Empty },
                { "operation", mySettings?.Operation ?? string.Empty },
            };

            //code-step: 1.7 - Convert rate-limit descriptor record to standard items array
            outputData[OutputFieldNameConstants.CONST_items] = WrapJsonIntoItems(data, nodeExecutionContext);

            //code-step: 1.8 - Write output (handles TargetDataPath writes + items downstream)
            return await WriteOutputData(ExecutionConstants.OutputPorts.Success, outputData, data, nodeExecutionContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            //code-step: 1.9 - Catch exceptions and return error with context
            return resultManager.SetResultAsError(ExecutionConstants.OutputPorts.Error, $"message/getTokenTransferRateLimit failed: {ex.Message}", this);
        }
    }
}
