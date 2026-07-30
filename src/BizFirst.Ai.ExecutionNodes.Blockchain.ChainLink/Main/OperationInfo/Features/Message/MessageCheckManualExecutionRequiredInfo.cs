namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-MSG05 — message/checkManualExecutionRequired.</summary>
internal sealed class MessageCheckManualExecutionRequiredInfo : BaseChainlinkOperationInfo
{
    public string MessageID { get; private set; } = string.Empty;

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        MessageID = reader.ReadConfigByKey("messageId") ?? string.Empty;
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["messageId"] = MessageID,
    };
}
