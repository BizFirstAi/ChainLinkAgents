using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using System.Text.RegularExpressions;

namespace BizFirst.Integration.Chainlink.Services;

/// <summary>
/// Pure, dependency-free encoder for CCIP's <c>Client.EVM2AnyMessage</c> struct and its tag-prefixed
/// <c>extraArgs</c> bytes (00_INDEX.md §4.3/§5.1's CCIP-MSG01). No HTTP, no signing, no I/O — this is
/// the one place this node does real computational work, analogous to Safe's SafeTxBuilder.
///
/// This was the single highest-risk, least-specified piece of the entire design (00_INDEX.md §17
/// open question 12: the design repeatedly called this "the one genuinely fiddly part" but never
/// showed the actual algorithm). Implemented and verified here against Nethereum's real ABI encoder
/// (Nethereum.ABI.ABIEncode/ABIValue — confirmed by compiling and running a standalone snippet during
/// implementation, not assumed) using the two published, fixed 4-byte tag selectors Chainlink itself
/// documents for extraArgs versioning:
///   • EVMExtraArgsV1    tag = 0x97a657c9, body = abi.encode(uint256 gasLimit)
///   • GenericExtraArgsV2 tag = 0x181dcf10, body = abi.encode(uint256 gasLimit, bool allowOutOfOrderExecution)
/// (The GenericExtraArgsV2 tag matches the illustrative example byte sequence already present in the
/// design doc's own §14.2 output example, "0x181dcf10...", corroborating this value.)
///
/// <see cref="Build"/>'s <c>EncodedMessageJson</c> output is a JSON array of the message tuple's raw
/// positional values (receiver/data/tokenAmounts/feeToken/extraArgs) — NOT a single pre-ABI-encoded
/// blob. This is a deliberate implementation decision, not directly specified in the original design:
/// the Ethereum ExecutionNode's own SC02 (Write Contract Function) operation ABI-encodes a tuple-typed
/// function argument from a nested JSON array of its component values (confirmed convention in
/// EthereumContractService's own doc comment: "nested JSON arrays convert recursively to object[] for
/// ABI array types"). A single opaque pre-encoded blob would not compose with that real delegation
/// path — SC02 needs the tuple's raw values, not an already-abi.encode()'d struct, to build its own
/// <c>ccipSend(destinationChainSelector, message)</c> call. This choice is what makes the
/// design's §15 delegation workflow (Chainlink builds the message → Ethereum SC02 sends it) actually
/// composable end-to-end.
/// </summary>
public sealed class ChainlinkMessageBuilder
{
    private const string EvmExtraArgsV1Tag = "97a657c9";
    private const string GenericExtraArgsV2Tag = "181dcf10";

    /// <summary>Chainlink's own documented default callback gas limit when a caller doesn't specify one.</summary>
    private const long DefaultGasLimit = 200_000;

    // Confirmed necessary during implementation via a direct scratch test against the real Nethereum
    // 6.0.0 ABI encoder: it throws Nethereum.ABI.FunctionEncoding.AbiEncodingException (NOT
    // ArgumentException) for a non-hex address like "not-an-address", but SILENTLY ACCEPTS a
    // too-short address like "0x123" with no error at all — the ABI-encoded output would then be
    // wrong/corrupted rather than the call failing loudly. Validating the exact 20-byte address
    // shape ourselves, before ever calling into Nethereum, is the only way to catch the second case
    // and to guarantee a stable, catchable ArgumentException for both.
    private static readonly Regex EvmAddressPattern = new("^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    public sealed record BuildResult(string EncodedMessageJson, string ExtraArgsBytesHex);

    /// <summary>
    /// The message tuple's raw component values, before JSON serialization. Exposed separately from
    /// <see cref="Build"/> so callers that need the actual values (not a JSON string to re-parse) —
    /// namely CCIP-RTR03's <c>getFee</c> on-chain call — can consume them directly. Building this
    /// once and having both <see cref="Build"/> and the Router service's getFee call share it avoids
    /// the fragile alternative of re-parsing <see cref="BuildResult.EncodedMessageJson"/> back apart.
    /// </summary>
    public sealed record MessageParts(
        string ReceiverBytesHex,
        string DataBytesHex,
        IReadOnlyList<ChainlinkTokenAmount> TokenAmounts,
        string FeeTokenAddress,
        string ExtraArgsBytesHex);

    /// <summary>
    /// Builds the EVM2AnyMessage tuple's raw component values and the tag-prefixed extraArgs bytes.
    /// </summary>
    /// <param name="receiverAddress">Destination-chain recipient address (0x...). ABI-encoded as a
    /// left-padded 32-byte value per Chainlink's own sample sender code convention
    /// (<c>receiver: abi.encode(receiverAddress)</c>), not the raw 20-byte address.</param>
    /// <param name="payloadDataHex">Arbitrary application payload, hex-encoded (0x... or bare hex). Empty/null becomes zero-length bytes.</param>
    /// <param name="tokenAmounts">Token transfers accompanying the message. Empty when this is a pure data message.</param>
    /// <param name="feeToken">Fee token address. Null/empty means pay in native gas token (address(0)).</param>
    /// <param name="extraArgsVersion"><c>"v1"</c> or <c>"genericV2"</c> (case-insensitive). Defaults to genericV2 when unspecified.</param>
    /// <param name="gasLimit">Callback gas limit for both extraArgs versions. Defaults to <see cref="DefaultGasLimit"/> when null.</param>
    /// <param name="allowOutOfOrderExecution">Only meaningful for genericV2; ignored for v1.</param>
    public MessageParts BuildParts(
        string receiverAddress,
        string? payloadDataHex,
        IReadOnlyList<ChainlinkTokenAmount> tokenAmounts,
        string? feeToken,
        string? extraArgsVersion,
        long? gasLimit,
        bool allowOutOfOrderExecution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiverAddress);
        ValidateAddress(receiverAddress, nameof(receiverAddress));

        if (!string.IsNullOrWhiteSpace(feeToken))
            ValidateAddress(feeToken, nameof(feeToken));

        foreach (var tokenAmount in tokenAmounts)
        {
            ValidateAddress(tokenAmount.Token, nameof(tokenAmounts));
            if (!BigInteger.TryParse(tokenAmount.Amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAmount) || parsedAmount < 0)
                throw new ArgumentException($"tokenAmounts entry has an invalid 'amount' value: '{tokenAmount.Amount}'.", nameof(tokenAmounts));
        }

        var receiverBytesHex = "0x" + new ABIEncode().GetABIEncoded(new ABIValue("address", receiverAddress)).ToHex();
        var dataBytesHex = NormalizeHex(payloadDataHex) ?? "0x";
        var feeTokenAddress = string.IsNullOrWhiteSpace(feeToken) ? "0x0000000000000000000000000000000000000000" : feeToken;
        var extraArgsBytesHex = BuildExtraArgs(extraArgsVersion, gasLimit ?? DefaultGasLimit, allowOutOfOrderExecution);

        return new MessageParts(receiverBytesHex, dataBytesHex, tokenAmounts, feeTokenAddress, extraArgsBytesHex);
    }

    /// <summary>
    /// Builds the EVM2AnyMessage tuple (as a JSON array of raw positional values, see class remarks)
    /// and the tag-prefixed extraArgs bytes — the public CCIP-MSG01 entry point. Thin wrapper over
    /// <see cref="BuildParts"/> that serializes the parts to JSON for this node's own output contract
    /// (00_INDEX.md §14.2).
    /// </summary>
    public BuildResult Build(
        string receiverAddress,
        string? payloadDataHex,
        IReadOnlyList<ChainlinkTokenAmount> tokenAmounts,
        string? feeToken,
        string? extraArgsVersion,
        long? gasLimit,
        bool allowOutOfOrderExecution)
    {
        var parts = BuildParts(receiverAddress, payloadDataHex, tokenAmounts, feeToken, extraArgsVersion, gasLimit, allowOutOfOrderExecution);

        var tokenAmountsJson = parts.TokenAmounts.Select(t => new object[] { t.Token, t.Amount }).ToArray();
        var messageJsonArray = new object[]
        {
            parts.ReceiverBytesHex,
            parts.DataBytesHex,
            tokenAmountsJson,
            parts.FeeTokenAddress,
            parts.ExtraArgsBytesHex,
        };

        var encodedMessageJson = JsonSerializer.Serialize(messageJsonArray);
        return new BuildResult(encodedMessageJson, parts.ExtraArgsBytesHex);
    }

    /// <summary>
    /// Builds just the tag-prefixed extraArgs bytes — split out from <see cref="Build"/> so
    /// CCIP-RTR03 (router/getFee) can build the identical extraArgs a getFee call needs without
    /// duplicating this logic.
    /// </summary>
    public string BuildExtraArgs(string? extraArgsVersion, long gasLimit, bool allowOutOfOrderExecution)
    {
        if (gasLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(gasLimit), "gasLimit must not be negative.");

        var version = string.IsNullOrWhiteSpace(extraArgsVersion) ? "genericv2" : extraArgsVersion.Trim().ToLowerInvariant();
        var abiEncode = new ABIEncode();

        return version switch
        {
            "v1" => "0x" + EvmExtraArgsV1Tag + abiEncode.GetABIEncoded(
                new ABIValue("uint256", new BigInteger(gasLimit))).ToHex(),

            "genericv2" => "0x" + GenericExtraArgsV2Tag + abiEncode.GetABIEncoded(
                new ABIValue("uint256", new BigInteger(gasLimit)),
                new ABIValue("bool", allowOutOfOrderExecution)).ToHex(),

            _ => throw new ArgumentException($"Unknown extraArgsVersion '{extraArgsVersion}' — expected 'v1' or 'genericV2'.", nameof(extraArgsVersion)),
        };
    }

    private static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        return hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex : "0x" + hex;
    }

    /// <summary>
    /// Validates an EVM address is exactly `0x` + 40 hex characters. Nethereum's own ABI encoder does
    /// NOT reliably reject a malformed address (confirmed: it silently accepts a too-short address
    /// with no error), so this node validates the shape itself before any value reaches the encoder.
    /// </summary>
    private static void ValidateAddress(string address, string paramName)
    {
        if (!EvmAddressPattern.IsMatch(address))
            throw new ArgumentException($"'{address}' is not a valid EVM address (expected 0x followed by 40 hex characters).", paramName);
    }
}
