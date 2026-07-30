namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-MSG02 — message/getStatus.</summary>
internal sealed class MessageGetStatusInfo : BaseChainlinkOperationInfo
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
