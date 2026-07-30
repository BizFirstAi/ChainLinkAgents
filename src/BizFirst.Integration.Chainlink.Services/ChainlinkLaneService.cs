namespace BizFirst.Integration.Chainlink.Services;

/// <summary>Lane resource: getSupportedLanes (GET chains), getLatency (GET lanes), getRiskManagementStatus (GET verifiers).</summary>
public sealed class ChainlinkLaneService
{
    private readonly ChainlinkCcipApiClient _apiClient;
    private readonly ILogger<ChainlinkLaneService> _logger;

    public ChainlinkLaneService(ChainlinkCcipApiClient apiClient, ILogger<ChainlinkLaneService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>CCIP-LNE01 — lane/getSupportedLanes. HTTP: GET chains.</summary>
    public async Task<ChainlinkLaneGetSupportedLanesResult> GetSupportedLanesAsync(string? sourceNetworkName, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = await _apiClient.GetChainsAsync(sourceNetworkName, credential, cancellationToken).ConfigureAwait(false);
            var lanes = ParseLanes(root, sourceNetworkName);
            return ChainlinkLaneGetSupportedLanesResult.Ok(lanes);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkApiException ex)
        {
            _logger.LogError(ex, "lane/getSupportedLanes failed for source network {SourceNetwork}", sourceNetworkName);
            var (code, message) = ChainlinkErrorMapper.Map(ex);
            return ChainlinkLaneGetSupportedLanesResult.Fail(code, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lane/getSupportedLanes failed unexpectedly for source network {SourceNetwork}", sourceNetworkName);
            return ChainlinkLaneGetSupportedLanesResult.Fail("CCIP_API_UPSTREAM_ERROR", ex.Message);
        }
    }

    /// <summary>CCIP-LNE02 — lane/getLatency. HTTP: GET lanes.</summary>
    public async Task<ChainlinkLaneGetLatencyResult> GetLatencyAsync(string sourceChainSelector, string destinationChainSelector, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = await _apiClient.GetLaneLatencyAsync(sourceChainSelector, destinationChainSelector, credential, cancellationToken).ConfigureAwait(false);

            double? latency = null;
            if (root.ValueKind == JsonValueKind.Object
                && ChainlinkCcipApiClient.TryGetAny(root, out var latencyElement, "latencyEstimateSeconds", "latencySeconds", "latency")
                && latencyElement.ValueKind == JsonValueKind.Number)
            {
                latency = latencyElement.GetDouble();
            }

            if (latency is not { } resolvedLatency)
                return ChainlinkLaneGetLatencyResult.Fail("LANE_NOT_SUPPORTED", $"No lane latency data found for {sourceChainSelector} -> {destinationChainSelector}, or the response shape did not match any known field name.");

            return ChainlinkLaneGetLatencyResult.Ok(resolvedLatency);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkApiException ex)
        {
            _logger.LogError(ex, "lane/getLatency failed for {Source} -> {Destination}", sourceChainSelector, destinationChainSelector);
            var (code, message) = ChainlinkErrorMapper.Map(ex);
            return ChainlinkLaneGetLatencyResult.Fail(code, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lane/getLatency failed unexpectedly for {Source} -> {Destination}", sourceChainSelector, destinationChainSelector);
            return ChainlinkLaneGetLatencyResult.Fail("CCIP_API_UPSTREAM_ERROR", ex.Message);
        }
    }

    /// <summary>CCIP-LNE03 — lane/getRiskManagementStatus. HTTP: GET verifiers.</summary>
    public async Task<ChainlinkLaneGetRiskManagementStatusResult> GetRiskManagementStatusAsync(string? chainSelector, ChainlinkApiCredential? credential, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = await _apiClient.GetVerifiersAsync(chainSelector, credential, cancellationToken).ConfigureAwait(false);

            string? status = null;
            if (root.ValueKind == JsonValueKind.Object
                && ChainlinkCcipApiClient.TryGetAny(root, out var statusElement, "status", "riskManagementStatus", "state")
                && statusElement.ValueKind == JsonValueKind.String)
            {
                status = statusElement.GetString();
            }
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                // Some list-shaped /verifiers responses report status per-entry rather than at the root —
                // treat any entry reporting an anomaly as the overall status (conservative: don't hide a
                // real anomaly by only looking at the first entry).
                var anyAnomaly = root.EnumerateArray().Any(entry =>
                    ChainlinkCcipApiClient.TryGetAny(entry, out var entryStatus, "status", "riskManagementStatus", "state")
                    && entryStatus.ValueKind == JsonValueKind.String
                    && !string.Equals(entryStatus.GetString(), "NORMAL", StringComparison.OrdinalIgnoreCase));
                status = anyAnomaly ? "ANOMALY_DETECTED" : "NORMAL";
            }

            return ChainlinkLaneGetRiskManagementStatusResult.Ok(status);
        }
        catch (OperationCanceledException) { throw; }
        catch (ChainlinkApiException ex)
        {
            _logger.LogError(ex, "lane/getRiskManagementStatus failed for chain selector {ChainSelector}", chainSelector);
            var (code, message) = ChainlinkErrorMapper.Map(ex);
            return ChainlinkLaneGetRiskManagementStatusResult.Fail(code, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lane/getRiskManagementStatus failed unexpectedly for chain selector {ChainSelector}", chainSelector);
            return ChainlinkLaneGetRiskManagementStatusResult.Fail("CCIP_API_UPSTREAM_ERROR", ex.Message);
        }
    }

    private static IReadOnlyList<ChainlinkLane> ParseLanes(JsonElement root, string? sourceNetworkName)
    {
        var lanes = new List<ChainlinkLane>();

        // The real response shape for GET chains was never fully reconfirmed (00_INDEX.md §17 open
        // question 13) — defensively accept either a bare array or an array wrapped in an envelope
        // object (e.g. {"chains": [...]} / {"data": [...]}), matching the same hedge already applied
        // to message/lane-status field-name lookups elsewhere in this project.
        var array = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when ChainlinkCcipApiClient.TryGetAny(root, out var wrapped, "chains", "data", "items", "results") && wrapped.ValueKind == JsonValueKind.Array => wrapped,
            _ => (JsonElement?)null,
        };
        if (array is not { } resolvedArray) return lanes;

        foreach (var entry in resolvedArray.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;

            var name = ReadString(entry, "name", "networkName", "displayName") ?? string.Empty;
            var chainSelector = ReadString(entry, "chainSelector") ?? string.Empty;
            if (string.IsNullOrEmpty(chainSelector)) continue;

            lanes.Add(new ChainlinkLane(
                SourceNetworkName: sourceNetworkName ?? string.Empty,
                SourceChainSelector: string.Empty,
                DestinationNetworkName: name,
                DestinationChainSelector: chainSelector));
        }

        return lanes;
    }

    private static string? ReadString(JsonElement element, params string[] candidateNames) =>
        ChainlinkCcipApiClient.TryGetAny(element, out var value, candidateNames) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
