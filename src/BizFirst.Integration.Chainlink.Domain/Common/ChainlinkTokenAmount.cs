namespace BizFirst.Integration.Chainlink.Domain;

/// <summary>
/// One entry of CCIP's <c>Client.EVMTokenAmount[] tokenAmounts</c> — a token address plus a uint256
/// amount. <see cref="Amount"/> is a decimal string (wei-denominated), not a numeric type, for the
/// same JS safe-integer reason as <see cref="ChainlinkNetworkInfo.ChainSelector"/>: a uint256 token
/// amount can trivially exceed 2^53-1.
/// </summary>
public sealed record ChainlinkTokenAmount(
    string Token,
    string Amount);
