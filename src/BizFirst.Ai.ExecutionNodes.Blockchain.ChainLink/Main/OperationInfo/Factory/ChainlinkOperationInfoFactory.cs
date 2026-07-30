namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>Maps (resource, operation) config values to the concrete operation DTO, then loads it from the raw config.</summary>
internal static class ChainlinkOperationInfoFactory
{
    internal static BaseChainlinkOperationInfo? Create(string? resource, string? operation, ConfigDataPropertyBag reader)
    {
        BaseChainlinkOperationInfo? info = (resource, operation) switch
        {
            ("message", "build") => new MessageBuildInfo(),
            ("message", "getStatus") => new MessageGetStatusInfo(),
            ("message", "checkManualExecutionRequired") => new MessageCheckManualExecutionRequiredInfo(),
            ("message", "getTokenTransferRateLimit") => new MessageGetTokenTransferRateLimitInfo(),

            ("router", "getAddress") => new RouterGetAddressInfo(),
            ("router", "isChainSupported") => new RouterIsChainSupportedInfo(),
            ("router", "getFee") => new RouterGetFeeInfo(),
            ("router", "convertChainSelector") => new RouterConvertChainSelectorInfo(),

            ("lane", "getSupportedLanes") => new LaneGetSupportedLanesInfo(),
            ("lane", "getLatency") => new LaneGetLatencyInfo(),
            ("lane", "getRiskManagementStatus") => new LaneGetRiskManagementStatusInfo(),

            _ => null,
        };

        info?.LoadFrom(reader);
        return info;
    }
}
