namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>Configuration management partial for <see cref="ChainlinkNodeExecutor"/>.</summary>
public sealed partial class ChainlinkNodeExecutor
{
    /// <summary>Returns a new <see cref="ChainlinkNodeExecutorSettings"/> instance so LoadConfigAsync gets typed access instead of DictionaryBasedSettings.</summary>
    public override BaseNodeExecutorSettings CreateExecutorSettings() => new ChainlinkNodeExecutorSettings();

    /// <summary>Typed settings accessor — casts this.settings to <see cref="ChainlinkNodeExecutorSettings"/>.</summary>
    private ChainlinkNodeExecutorSettings? mySettings => (ChainlinkNodeExecutorSettings?)this.settings;

    private NodeResultOperateManager? _executionResultManager;
    private NodeResultOperateManager resultManager => _executionResultManager!;

    /// <summary>Initialises the standard success ("main") / error output ports on this node.</summary>
    public override void ValidateExecutorSettings()
    {
        if (mySettings?.OutputMapping is null) return;
        mySettings.OutputMapping.GetOrCreatePortSuccessAndError();
    }

    protected override NodeExecutorManifest? GetNodeExecutorManifest()
        => NodeExecutorManifest.From(
            ProcessElementTypeCode,
            [],
            new SuspensionPolicy { AllowAdminForceComplete = true });
}
