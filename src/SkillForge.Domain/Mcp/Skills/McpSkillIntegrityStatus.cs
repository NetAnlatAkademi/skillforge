namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// What could be established about one skill's content.
/// </summary>
/// <remarks>
/// Integrity only. None of these values says anything about whether the skill is trusted, safe, or published by whom
/// it claims: a server that rewrites a file and its digest together stays perfectly consistent. Those are provenance
/// and policy questions, and each has its own command.
/// </remarks>
public enum McpSkillIntegrityStatus
{
    /// <summary>
    /// Every file the manifest names was read, its bytes matched the declared digest and size, and the frontmatter
    /// the server described agreed with the one in <c>SKILL.md</c>.
    /// </summary>
    IntegrityMatch = 0,

    /// <summary>At least one file, the frontmatter, or the manifest boundary disagreed with what was declared.</summary>
    IntegrityMismatch,

    /// <summary>The manifest is <c>"dynamic"</c>, so there is no declared content to compare against.</summary>
    NotEstablished,

    /// <summary>The manifest is malformed, so nothing was read: a host must not load a skill on such a manifest.</summary>
    InvalidManifest,

    /// <summary><c>skills/get</c> did not answer, so there was no manifest to verify.</summary>
    Unavailable,
}
