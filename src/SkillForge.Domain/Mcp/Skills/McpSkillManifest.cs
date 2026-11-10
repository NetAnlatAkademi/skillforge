namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// A skill's resource manifest: for a static skill, the set of files a reviewer approves when they approve the skill.
/// </summary>
/// <param name="Kind">Static, dynamic, or neither.</param>
/// <param name="Resources">The files a static manifest names, in the order the server listed them. Empty otherwise.</param>
/// <param name="Detail">Why the manifest is invalid, when it is. <see langword="null"/> otherwise.</param>
public sealed record McpSkillManifest(
    McpSkillManifestKind Kind,
    IReadOnlyList<McpSkillResource> Resources,
    string? Detail = null)
{
    /// <summary>Gets the manifest of a skill whose content is generated.</summary>
    public static McpSkillManifest Dynamic { get; } = new(McpSkillManifestKind.Dynamic, []);

    /// <summary>A static manifest.</summary>
    /// <param name="resources">The files it names.</param>
    /// <returns>The manifest.</returns>
    public static McpSkillManifest Static(IReadOnlyList<McpSkillResource> resources) =>
        new(McpSkillManifestKind.Static, resources);

    /// <summary>A manifest that is neither an array nor <c>"dynamic"</c>.</summary>
    /// <param name="detail">What it was instead.</param>
    /// <returns>The manifest.</returns>
    public static McpSkillManifest Invalid(string detail) => new(McpSkillManifestKind.Invalid, [], detail);
}
