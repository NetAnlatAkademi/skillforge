namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// One skill as <c>skills/list</c> or <c>skills/get</c> described it.
/// </summary>
/// <remarks>
/// The frontmatter is kept as the JSON text the server sent rather than as a parsed model, so the Domain stays free of
/// any JSON library and the one frontmatter parser SkillForge has reads it. JSON is valid YAML, which is what lets a
/// frontmatter served over MCP and one read out of a <c>SKILL.md</c> go through the same parser.
/// </remarks>
/// <param name="Uri">The URI of the skill's <c>SKILL.md</c>.</param>
/// <param name="FrontmatterJson">The <c>frontmatter</c> object as compact JSON, or <see langword="null"/> when absent.</param>
/// <param name="Manifest">The resource manifest.</param>
/// <param name="TtlMs">The <c>ttlMs</c> freshness hint of the result that carried the entry, when one was sent.</param>
/// <param name="CacheScope">The <c>cacheScope</c> of the result that carried the entry, when one was sent.</param>
public sealed record McpSkillEntry(
    string Uri,
    string? FrontmatterJson,
    McpSkillManifest Manifest,
    long? TtlMs = null,
    string? CacheScope = null);
