namespace SkillForge.Domain.Mcp;

/// <summary>
/// How a <c>tools/list</c> enumeration ended.
/// </summary>
/// <remarks>
/// Three outcomes rather than a boolean, because a count that stopped early and a count that finished must not look
/// alike. A tool surface read to the end is a total; one cut short by a guard is a floor, and every number derived
/// from it — the surface risk, a declared-versus-runtime comparison — is a floor too.
/// </remarks>
public enum McpToolPagingOutcome
{
    /// <summary>Every page was read: the last response carried no <c>nextCursor</c>.</summary>
    Complete = 0,

    /// <summary>
    /// SkillForge stopped at its own page limit. Not a fault in the server — a bound on how much one inspection
    /// will ask for — but the tool list is incomplete and is reported as such.
    /// </summary>
    PageLimitReached,

    /// <summary>
    /// The server handed back a cursor it had already given out, so following it would never end. The pages read
    /// before the repeat are kept; the enumeration is not complete.
    /// </summary>
    CursorLoopDetected,
}

/// <summary>
/// What it took to read a server's whole tool list.
/// </summary>
/// <param name="PagesRead">How many <c>tools/list</c> responses were read. At least one when the server answered.</param>
/// <param name="FirstPageToolCount">
/// How many tools the first response carried, before deduplication. This is what an agent has in context after one
/// round trip, which is a different number from the total for any server that pages — and the same number for every
/// server that does not.
/// </param>
/// <param name="Outcome">Whether the enumeration finished, and if not, why it stopped.</param>
public sealed record McpToolPaging(int PagesRead, int FirstPageToolCount, McpToolPagingOutcome Outcome)
{
    /// <summary>Gets a value indicating whether the whole tool list was read.</summary>
    public bool IsComplete => Outcome == McpToolPagingOutcome.Complete;
}
