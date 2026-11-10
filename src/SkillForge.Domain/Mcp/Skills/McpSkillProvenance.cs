namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// Where an MCP-served skill came from, as far as a probe could observe.
/// </summary>
/// <remarks>
/// The MCP counterpart of the distribution evidence <c>provenance</c> records for an asset on disk: the source is a
/// server, not a directory, and the content baseline is the manifest's digests, not a fingerprint of local files.
///
/// **The server's identity is not verified, and nothing here claims it is.** The endpoint is the URL the configuration
/// named; the name and version are what the server said about itself, which the specification says clients must not
/// use for security decisions. The manifest digests are unsigned, so they bind content to a manifest and not to a
/// publisher.
/// </remarks>
/// <param name="ServerName">The name the configuration gives the server.</param>
/// <param name="Endpoint">The URL that was asked, with any credentials in it removed.</param>
/// <param name="SelfReportedName">The server's <c>serverInfo.name</c>, self-reported.</param>
/// <param name="SelfReportedVersion">The server's <c>serverInfo.version</c>, self-reported.</param>
/// <param name="ProtocolVersion">The protocol revision the server answered in.</param>
/// <param name="SkillUri">The URI of the skill's <c>SKILL.md</c>.</param>
/// <param name="Manifest">The manifest's files with their declared digests and sizes. Empty for a dynamic skill.</param>
/// <param name="ObservedAt">When the manifest was read.</param>
public sealed record McpSkillProvenance(
    string ServerName,
    string? Endpoint,
    string? SelfReportedName,
    string? SelfReportedVersion,
    string? ProtocolVersion,
    string SkillUri,
    IReadOnlyList<McpSkillResource> Manifest,
    DateTimeOffset ObservedAt)
{
    /// <summary>The kind of source this provenance describes. Always an MCP server.</summary>
    public const string Origin = "McpServer";
}
