namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// The direct children of one directory, read to the end or as far as the bounds allowed.
/// </summary>
/// <param name="Entries">The children, ordered by URI.</param>
/// <param name="Outcome">Whether every page was read.</param>
public sealed record McpDirectoryListing(IReadOnlyList<McpDirectoryEntry> Entries, McpToolPagingOutcome Outcome);
