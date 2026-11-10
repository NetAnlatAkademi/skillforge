namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// What <c>skills/list</c> returned, and what it took to read it.
/// </summary>
/// <remarks>
/// The same bounded walk as <c>tools/list</c>, so the same three outcomes. The specification allows a listing to be
/// partial, and a skill missing from it can still be fetched by URI, so an incomplete walk is reported and a short
/// listing is never read as "this server has no other skills".
/// </remarks>
/// <param name="Skills">One entry per URI, ordered by URI. The first entry for a repeated URI wins.</param>
/// <param name="PagesRead">How many responses were read.</param>
/// <param name="Outcome">Whether the walk finished, and if not, why it stopped.</param>
/// <param name="MalformedEntries">
/// Entries skipped because they named no URI. Counted rather than dropped silently: one bad entry does not take the
/// listing down with it, and it does not vanish either.
/// </param>
/// <param name="DuplicateEntries">Entries skipped because an earlier one had the same URI.</param>
/// <param name="Detail">Why a page could not be read, when one could not. Pages read before it are kept.</param>
public sealed record McpSkillListing(
    IReadOnlyList<McpSkillEntry> Skills,
    int PagesRead,
    McpToolPagingOutcome Outcome,
    int MalformedEntries = 0,
    int DuplicateEntries = 0,
    string? Detail = null)
{
    /// <summary>Gets a value indicating whether every page was read.</summary>
    public bool IsComplete => Outcome == McpToolPagingOutcome.Complete && Detail is null;
}
