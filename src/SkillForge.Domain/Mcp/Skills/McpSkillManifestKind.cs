namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// What a skill's <c>resources</c> field turned out to be.
/// </summary>
public enum McpSkillManifestKind
{
    /// <summary>An array of files, each with a digest and a size: content that can be checked byte for byte.</summary>
    Static = 0,

    /// <summary>The string <c>"dynamic"</c>: generated content with no stable digests. Not invalid, and not checkable.</summary>
    Dynamic,

    /// <summary>Neither of the two forms the specification defines, so the manifest says nothing usable.</summary>
    Invalid,
}
