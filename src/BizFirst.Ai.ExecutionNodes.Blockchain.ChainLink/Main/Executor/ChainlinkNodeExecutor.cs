namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>
/// Chainlink ExecutionNode — routes all resource/operation combinations to feature partials.
/// Implements the reviewed design spec (Documentation/.../010_NodeDesign-Engineer/ExecutionNodes/
/// chainlink/44_Features/Design/00_INDEX.md, v1.4): 11 operations across 3 resources (Message,
/// Router, Lane) against Chainlink's CCIP (Cross-Chain Interoperability Protocol). Data Feeds is
/// out of scope for this node (delegates entirely to the Ethereum ExecutionNode's generic
/// contract-call path + Standards Registry); VRF/Automation/Functions are explicitly deferred — see
/// the design doc §1/§2. Two operations present in the original design (`CCIP-MSG03` Get Message by
/// Transaction, `CCIP-MSG04` List Messages by Address) were cut from this implementation per the
/// design's own §17 open question 2 recommendation — a deep design review found no real CCIP API
/// endpoint backs either.
///
/// Design:
///   ChainlinkNodeExecutor.cs             — routing switch + constructor        (this file)
///   ChainlinkNodeExecutor.Config.cs       — settings accessor + port initialisation + manifest
///   ChainlinkNodeExecutor.Credentials.cs  — optional CCIP API key vault resolution
///   Main/Executor/Features/{Resource}/{Operation}/ — one feature partial per operation
///
/// Registration: BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink.ChainlinkDependency (Support/).
/// </summary>
public sealed partial class ChainlinkNodeExecutor : ResourceBasedNodeExecutor, IActionNodeExecution
{
    public const string NodeTypeName = "chainlink";
    public override string ProcessElementTypeCode => NodeTypeName;

    private readonly ChainlinkMessageService _messageService;
    private readonly ChainlinkRouterService _routerService;
    private readonly ChainlinkLaneService _laneService;

    public ChainlinkNodeExecutor(
        ILogger<ChainlinkNodeExecutor> logger,
        ChainlinkMessageService messageService,
        ChainlinkRouterService routerService,
        ChainlinkLaneService laneService,
        INodeCredentialsFactory? credentialsFactory = null,
        INodeConfigurationParser? configParser = null,
        NodeFormResolver? formResolver = null)
        : base(logger, credentialsFactory, configParser, formResolver)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _routerService = routerService ?? throw new ArgumentNullException(nameof(routerService));
        _laneService = laneService ?? throw new ArgumentNullException(nameof(laneService));
    }

    protected override async Task<NodeExecutionResult> _ExecuteInternal_Route_Async(
        NodeExecutionContext nodeExecutionContext,
        CancellationToken cancellationToken = default)
    {
        _executionResultManager = NodeResultOperateManager.CreateInstance(nodeExecutionContext);

        var result = (mySettings!.Resource, mySettings!.Operation) switch
        {
            // ── message (4) ──────────────────────────────────────────────────
            ("message", "build") => await _Chainlink_Message_BuildAsync(nodeExecutionContext, cancellationToken),
            ("message", "getStatus") => await _Chainlink_Message_GetStatusAsync(nodeExecutionContext, cancellationToken),
            ("message", "checkManualExecutionRequired") => await _Chainlink_Message_CheckManualExecutionRequiredAsync(nodeExecutionContext, cancellationToken),
            ("message", "getTokenTransferRateLimit") => await _Chainlink_Message_GetTokenTransferRateLimitAsync(nodeExecutionContext, cancellationToken),

            // ── router (4) ───────────────────────────────────────────────────
            ("router", "getAddress") => await _Chainlink_Router_GetAddressAsync(nodeExecutionContext, cancellationToken),
            ("router", "isChainSupported") => await _Chainlink_Router_IsChainSupportedAsync(nodeExecutionContext, cancellationToken),
            ("router", "getFee") => await _Chainlink_Router_GetFeeAsync(nodeExecutionContext, cancellationToken),
            ("router", "convertChainSelector") => await _Chainlink_Router_ConvertChainSelectorAsync(nodeExecutionContext, cancellationToken),

            // ── lane (3) ─────────────────────────────────────────────────────
            ("lane", "getSupportedLanes") => await _Chainlink_Lane_GetSupportedLanesAsync(nodeExecutionContext, cancellationToken),
            ("lane", "getLatency") => await _Chainlink_Lane_GetLatencyAsync(nodeExecutionContext, cancellationToken),
            ("lane", "getRiskManagementStatus") => await _Chainlink_Lane_GetRiskManagementStatusAsync(nodeExecutionContext, cancellationToken),

            _ => await base._ExecuteInternal_Route_Async(nodeExecutionContext, cancellationToken)
        };

        LogActivity_Status($"{NodeTypeName} operation executed", result.IsSuccess);
        return result;
    }
}
