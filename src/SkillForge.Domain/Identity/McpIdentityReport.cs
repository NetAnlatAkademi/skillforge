using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Identity;

/// <summary>
/// One MCP server, and the identity it is reached with.
/// </summary>
/// <param name="ServerName">The name the configuration gives the server.</param>
/// <param name="SourcePath">The configuration file it was declared in.</param>
/// <param name="Identity">What could be established about how it is authorised against.</param>
/// <param name="Risk">How much that is worth stopping for.</param>
/// <param name="Reason">One sentence saying which combination produced that risk.</param>
/// <param name="Evidence">
/// The names the inference was drawn from, ordered: environment variables, headers, and what a probe's challenge
/// said. Never a value.
/// </param>
public sealed record McpServerIdentity(
    string ServerName,
    string SourcePath,
    AgentIdentity Identity,
    IdentityRisk Risk,
    string Reason,
    IReadOnlyList<string> Evidence);

/// <summary>
/// What <c>identity inspect</c> found in one configuration.
/// </summary>
/// <param name="Path">The file that was read.</param>
/// <param name="Servers">The servers it declares, with their identities, ordered by name.</param>
/// <param name="Diagnostics">What SkillForge could not read, and why.</param>
public sealed record McpIdentityReport(
    string Path,
    IReadOnlyList<McpServerIdentity> Servers,
    IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// What changed about the identities two configurations use.
/// </summary>
/// <param name="BeforePath">The earlier file.</param>
/// <param name="AfterPath">The later file.</param>
/// <param name="Changed">Servers in both files whose identity is not the same.</param>
/// <param name="Added">Servers only the later file declares.</param>
/// <param name="Removed">Servers only the earlier file declares.</param>
public sealed record IdentityDiff(
    string BeforePath,
    string AfterPath,
    IReadOnlyList<ServerIdentityChange> Changed,
    IReadOnlyList<McpServerIdentity> Added,
    IReadOnlyList<McpServerIdentity> Removed)
{
    /// <summary>Gets a value indicating whether anything at all is different.</summary>
    public bool HasChanges => Changed.Count > 0 || Added.Count > 0 || Removed.Count > 0;
}

/// <summary>
/// One server, and what changed about the identity it is reached with.
/// </summary>
/// <param name="ServerName">The server's name.</param>
/// <param name="Before">What the earlier configuration implied.</param>
/// <param name="After">What the later one implies.</param>
/// <param name="Type">Identity type change.</param>
/// <param name="Issuer">Issuer change.</param>
/// <param name="Delegation">Delegation change, written as <c>false</c> and <c>true</c>.</param>
/// <param name="Lifetime">Credential lifetime change, written as <c>short-lived</c> and <c>long-lived</c>.</param>
/// <param name="Scopes">Scopes added and removed.</param>
public sealed record ServerIdentityChange(
    string ServerName,
    McpServerIdentity Before,
    McpServerIdentity After,
    SurfaceValueChange? Type,
    SurfaceValueChange? Issuer,
    SurfaceValueChange? Delegation,
    SurfaceValueChange? Lifetime,
    SurfaceSetDiff Scopes)
{
    /// <summary>Gets a value indicating whether anything about this server's identity changed.</summary>
    public bool HasChanges =>
        Type is not null
        || Issuer is not null
        || Delegation is not null
        || Lifetime is not null
        || Scopes.HasChanges;
}
