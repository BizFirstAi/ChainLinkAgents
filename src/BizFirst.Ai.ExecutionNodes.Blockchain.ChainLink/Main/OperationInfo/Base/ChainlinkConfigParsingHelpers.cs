using System.Text.Json.Nodes;

namespace BizFirst.Ai.ExecutionNodes.Blockchain.Chainlink;

/// <summary>Shared config-value parsing for operation-info DTOs.</summary>
internal static class ChainlinkConfigParsingHelpers
{
    /// <summary>
    /// Parses the <c>tokenAmounts</c> config key — a JSON array of <c>{ "token": "0x...", "amount": "..." }</c>
    /// (00_INDEX.md §13.2) — via <see cref="ConfigDataPropertyBag.ReadConfigNodeByKey"/>, since
    /// ConfigDataPropertyBag has no dedicated typed-array reader. Malformed/missing entries are
    /// skipped rather than throwing, since <c>tokenAmounts</c> is optional (a pure data message has none).
    /// </summary>
    public static List<ChainlinkTokenAmount> ParseTokenAmounts(ConfigDataPropertyBag reader, string key)
    {
        var result = new List<ChainlinkTokenAmount>();
        var node = reader.ReadConfigNodeByKey(key);
        if (node is not JsonArray array) return result;

        foreach (var entry in array)
        {
            // Confirmed necessary during review: JsonNode.GetValue<string>() THROWS
            // InvalidOperationException/FormatException if the underlying value isn't a JSON string
            // (e.g. a workflow author writes "amount": 1000 as a bare number instead of a quoted
            // string) — this doc comment already claimed malformed entries are "skipped rather than
            // throwing," but the original implementation did not actually guarantee that. Fixed to
            // genuinely never throw: an entry that can't be read as a string/number is skipped, not
            // allowed to propagate an exception out of config loading.
            if (entry is not JsonObject obj) continue;

            var token = TryReadStringOrNumber(obj["token"]);
            var amount = TryReadStringOrNumber(obj["amount"]);
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(amount)) continue;

            result.Add(new ChainlinkTokenAmount(token, amount));
        }

        return result;
    }

    /// <summary>Reads a JsonNode as a string, accepting either a JSON string or a JSON number (formatted as its literal text) — never throws.</summary>
    private static string? TryReadStringOrNumber(JsonNode? node)
    {
        if (node is not JsonValue value) return null;

        if (value.TryGetValue<string>(out var stringValue)) return stringValue;
        if (value.TryGetValue<long>(out var longValue)) return longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetValue<double>(out var doubleValue)) return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return null;
    }
}
