using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Updates;

/// <summary>
/// Everything a tree of assets can reach, as far as its files declare.
/// </summary>
/// <remarks>
/// The unit an update is judged in. A version number says a plugin changed; this says whether what changed can now
/// run a shell command, reach a host or read a credential that it could not before — which is the question a
/// reviewer is actually asking when a plugin updates itself.
///
/// Names only, everywhere. <see cref="CredentialSources"/> holds the names of environment variables a declaration
/// reads, never their values: this type exists to be printed, and a report that leaks a token has done more harm
/// than the update it was warning about.
/// </remarks>
/// <param name="Skills">Skill names found in the tree.</param>
/// <param name="McpServers">MCP servers declared by any configuration in the tree.</param>
/// <param name="Hooks">Hook files found, by path relative to the tree.</param>
/// <param name="Scripts">Executable scripts found, by path relative to the tree.</param>
/// <param name="Domains">Hosts anything in the tree points at.</param>
/// <param name="CredentialSources">Names of environment variables the MCP declarations read.</param>
/// <param name="Capabilities">Capability names the skills' contents imply, such as shell execution.</param>
public sealed record CapabilitySurface(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> McpServers,
    IReadOnlyList<string> Hooks,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> CredentialSources,
    IReadOnlyList<string> Capabilities)
{
    /// <summary>A surface that reaches nothing.</summary>
    public static CapabilitySurface Empty { get; } = new([], [], [], [], [], [], []);
}

/// <summary>
/// What one tree can reach that the other could not.
/// </summary>
/// <param name="Skills">Skills added and removed.</param>
/// <param name="McpServers">MCP servers added and removed.</param>
/// <param name="Hooks">Hook files added and removed.</param>
/// <param name="Scripts">Scripts added and removed.</param>
/// <param name="Domains">Hosts added and removed.</param>
/// <param name="CredentialSources">Credential-shaped environment variable names added and removed.</param>
/// <param name="Capabilities">Implied capabilities added and removed.</param>
public sealed record CapabilitySurfaceDiff(
    SurfaceSetDiff Skills,
    SurfaceSetDiff McpServers,
    SurfaceSetDiff Hooks,
    SurfaceSetDiff Scripts,
    SurfaceSetDiff Domains,
    SurfaceSetDiff CredentialSources,
    SurfaceSetDiff Capabilities)
{
    /// <summary>Gets a value indicating whether anything at all changed.</summary>
    public bool HasChanges =>
        Skills.HasChanges
        || McpServers.HasChanges
        || Hooks.HasChanges
        || Scripts.HasChanges
        || Domains.HasChanges
        || CredentialSources.HasChanges
        || Capabilities.HasChanges;

    /// <summary>
    /// Gets a value indicating whether the update can reach further than what it replaced.
    /// </summary>
    /// <remarks>
    /// Additions only. An update that gave something up is a change worth showing and is not an expansion, and
    /// treating the two the same is how a report about growth starts warning about cleanups.
    /// </remarks>
    public bool Expands =>
        Skills.Added.Count > 0
        || McpServers.Added.Count > 0
        || Hooks.Added.Count > 0
        || Scripts.Added.Count > 0
        || Domains.Added.Count > 0
        || CredentialSources.Added.Count > 0
        || Capabilities.Added.Count > 0;

    /// <summary>Compares two surfaces.</summary>
    /// <param name="before">The earlier surface.</param>
    /// <param name="after">The later surface.</param>
    /// <returns>The difference, every list ordered.</returns>
    public static CapabilitySurfaceDiff Between(CapabilitySurface before, CapabilitySurface after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return new CapabilitySurfaceDiff(
            SurfaceSetDiff.Between(before.Skills, after.Skills),
            SurfaceSetDiff.Between(before.McpServers, after.McpServers),
            SurfaceSetDiff.Between(before.Hooks, after.Hooks),
            SurfaceSetDiff.Between(before.Scripts, after.Scripts),
            SurfaceSetDiff.Between(before.Domains, after.Domains),
            SurfaceSetDiff.Between(before.CredentialSources, after.CredentialSources),
            SurfaceSetDiff.Between(before.Capabilities, after.Capabilities));
    }
}
