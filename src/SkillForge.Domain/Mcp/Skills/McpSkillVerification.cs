namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// Everything established about one MCP-served skill.
/// </summary>
/// <param name="SkillUri">The URI of the skill's <c>SKILL.md</c>.</param>
/// <param name="Name">The frontmatter <c>name</c> the server described, when it described one.</param>
/// <param name="Listed">
/// Whether <c>skills/list</c> carried the skill. A skill that is fetched by URI without being listed is normal — the
/// specification requires servers to answer <c>skills/get</c> for every skill they serve — so this is recorded, not
/// judged.
/// </param>
/// <param name="Status">What could be established about its content.</param>
/// <param name="Resources">One check per manifest file, in manifest order.</param>
/// <param name="FrontmatterDrift">The frontmatter fields that disagree, as <c>field: described · in SKILL.md</c>.</param>
/// <param name="UnlistedResources">Files the directory listing showed that the manifest does not name, ordered.</param>
/// <param name="DirectoryEnumeration">
/// How the skill's directory was enumerated: <c>complete</c>, <c>partial</c>, <c>not declared</c> by the server, or
/// <c>not attempted</c>. Without a complete enumeration, "no unlisted resources" is not a claim this report can make.
/// </param>
/// <param name="Provenance">Where the skill came from. <see langword="null"/> when <c>skills/get</c> did not answer.</param>
/// <param name="TtlMs">The <c>ttlMs</c> of the <c>skills/get</c> result.</param>
/// <param name="CacheScope">The <c>cacheScope</c> of the <c>skills/get</c> result.</param>
/// <param name="Detail">Why the skill could not be verified, when it could not.</param>
public sealed record McpSkillVerification(
    string SkillUri,
    string? Name,
    bool Listed,
    McpSkillIntegrityStatus Status,
    IReadOnlyList<McpSkillResourceCheck> Resources,
    IReadOnlyList<string> FrontmatterDrift,
    IReadOnlyList<string> UnlistedResources,
    string DirectoryEnumeration,
    McpSkillProvenance? Provenance,
    long? TtlMs = null,
    string? CacheScope = null,
    string? Detail = null);
