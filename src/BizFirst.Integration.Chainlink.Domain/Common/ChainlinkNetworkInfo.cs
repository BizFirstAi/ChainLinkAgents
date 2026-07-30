namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// Identifies one side (source or destination) of a CCIP message. Mirrors the real CCIP API v2's
/// nested <c>sourceNetworkInfo</c>/<c>destNetworkInfo</c> response shape (confirmed via a fetch of
/// docs.chain.link/ccip/tools/api against the design review's own found response schema) rather than
/// flat top-level fields.
///
/// <see cref="ChainSelector"/> is a <see cref="string"/>, deliberately not a numeric type. CCIP chain
/// selectors are 64-bit values (e.g. Ethereum mainnet's <c>5009297550715157269</c>) that already
/// exceed JavaScript's safe-integer boundary (2^53-1 = 9,007,199,254,740,991) by roughly 500x — the
/// real CCIP API itself returns this field as a JSON string for exactly that reason. Encoding it as a
/// number anywhere in this node's own config/output contract would risk silent precision loss the
/// moment it round-trips through any JS-based consumer (Flow Studio's canvas, a browser config editor).
/// </summary>
public sealed record ChainlinkNetworkInfo(
    string Name,
    string ChainID,
    string ChainSelector);
