namespace SkillForge.Domain.Mcp;

/// <summary>
/// What a server's <c>401</c> said about how to authorise against it.
/// </summary>
/// <remarks>
/// This is the whole of the "authorization method" question that can be answered without credentials, and it is a
/// genuine answer: the challenge names the scheme, points at the Protected Resource Metadata document a client must use
/// to find the authorization server, and may name the scopes the operation needs.
///
/// SkillForge stops there. It does not fetch the metadata document or the authorization server's own metadata: those are
/// further requests to further hosts to report configuration belonging to neither the skill nor the MCP server, and
/// "one request per server" is a property worth keeping.
/// </remarks>
/// <param name="Scheme">The challenge scheme, normally <c>Bearer</c>.</param>
/// <param name="ResourceMetadataUrl">
/// The <c>resource_metadata</c> parameter — where the server's OAuth 2.0 Protected Resource Metadata lives.
/// <see langword="null"/> when the challenge omits it, which is itself a finding: MCP servers **MUST** implement
/// RFC 9728 and clients **MUST** use it for authorization server discovery, so a challenge without it leaves a
/// conforming client with nowhere to go.
/// </param>
/// <param name="Scope">The <c>scope</c> parameter, when the server names the scopes it wants.</param>
public sealed record McpAuthorizationChallenge(string Scheme, string? ResourceMetadataUrl, string? Scope);
