using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Speaks the <c>io.modelcontextprotocol/skills</c> extension to one HTTP server.
/// </summary>
/// <remarks>
/// A transport, not a skill format: what comes back is materialised into SkillForge's own model and read by the parser
/// that already reads <c>SKILL.md</c> on disk. Nothing here parses YAML.
///
/// URIs are strings rather than <see cref="Uri"/> on purpose. A manifest is compared byte for byte against what a
/// directory listing returns, and <see cref="Uri"/> normalises — case, escaping, a trailing slash — which would make two
/// different strings compare equal and hide exactly the boundary this exists to check.
///
/// Only an HTTP server is ever asked, and only after a caller has seen it declare the extension. None of these methods
/// throws for a server that fails; they throw only when the caller cancels.
/// </remarks>
public interface IMcpSkillsClient
{
    /// <summary>Reads every page of <c>skills/list</c>, under the same bounds as <c>tools/list</c>.</summary>
    /// <param name="server">An HTTP server that declared the extension.</param>
    /// <param name="cancellationToken">Checked before every page.</param>
    /// <returns>The listing, with how the walk ended.</returns>
    Task<McpSkillListing> ListAsync(McpServerDeclaration server, CancellationToken cancellationToken = default);

    /// <summary>Asks <c>skills/get</c> for one skill by the URI of its <c>SKILL.md</c>.</summary>
    /// <param name="server">An HTTP server that declared the extension.</param>
    /// <param name="skillUri">The skill's <c>SKILL.md</c> URI. It need not appear in <c>skills/list</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The entry, or why there is none.</returns>
    Task<McpResponse<McpSkillEntry>> GetAsync(
        McpServerDeclaration server,
        string skillUri,
        CancellationToken cancellationToken = default);

    /// <summary>Asks <c>resources/read</c> for one file's raw bytes.</summary>
    /// <param name="server">The server the skill came from. Reads never cross to another origin.</param>
    /// <param name="resourceUri">A URI the held manifest names.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The content, or why there is none.</returns>
    Task<McpResponse<McpResourceContent>> ReadAsync(
        McpServerDeclaration server,
        string resourceUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks <c>resources/directory/read</c> for one directory's direct children, every page of them.
    /// </summary>
    /// <param name="server">A server whose extension declaration set <c>directoryRead</c>. Never called otherwise.</param>
    /// <param name="directoryUri">The directory, with no trailing slash.</param>
    /// <param name="cancellationToken">Checked before every page.</param>
    /// <returns>The children, or why there are none.</returns>
    Task<McpResponse<McpDirectoryListing>> ReadDirectoryAsync(
        McpServerDeclaration server,
        string directoryUri,
        CancellationToken cancellationToken = default);
}
