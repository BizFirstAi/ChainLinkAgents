namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>CCIP-RTR03 — router/getFee. Takes the same message-building inputs as CCIP-MSG01, since computing a fee requires the same EVM2AnyMessage tuple.</summary>
internal sealed class RouterGetFeeInfo : BaseChainlinkOperationInfo
{
    public string NetworkName { get; private set; } = string.Empty;
    public string DestinationChainSelector { get; private set; } = string.Empty;
    public string ReceiverAddress { get; private set; } = string.Empty;
    public string? PayloadData { get; private set; }
    public List<ChainlinkTokenAmount> TokenAmounts { get; private set; } = new();
    public string? FeeToken { get; private set; }
    public string ExtraArgsVersion { get; private set; } = "genericV2";
    public long? GasLimit { get; private set; }
    public bool AllowOutOfOrderExecution { get; private set; }

    public override void LoadFrom(ConfigDataPropertyBag reader)
    {
        NetworkName = reader.ReadConfigByKey("network", "ethereum-mainnet") ?? "ethereum-mainnet";
        DestinationChainSelector = reader.ReadConfigByKey("destinationChainSelector") ?? string.Empty;
        ReceiverAddress = reader.ReadConfigByKey("receiverAddress") ?? string.Empty;
        PayloadData = reader.ReadConfigByKeyDefaultNull("payloadData");
        TokenAmounts = ChainlinkConfigParsingHelpers.ParseTokenAmounts(reader, "tokenAmounts");
        FeeToken = reader.ReadConfigByKeyDefaultNull("feeToken");
        ExtraArgsVersion = reader.ReadConfigByKey("extraArgsVersion", "genericV2") ?? "genericV2";
        GasLimit = reader.ReadConfigByKey_Long("gasLimit");
        AllowOutOfOrderExecution = reader.ReadConfigByKeyBool("allowOutOfOrderExecution");
    }

    public override Dictionary<string, object> ToDictionary() => new()
    {
        ["network"] = NetworkName,
        ["destinationChainSelector"] = DestinationChainSelector,
        ["receiverAddress"] = ReceiverAddress,
    };
}
