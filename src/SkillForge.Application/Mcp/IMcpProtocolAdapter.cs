using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Speaks one revision of the MCP protocol.
/// </summary>
/// <remarks>
/// The core is not bound to a protocol version — roadmap §30.6 — because MCP changes faster than this tool will. A
/// revision that alters the handshake gets its own adapter beside this one, and nothing above them changes.
///
/// Only servers reached over HTTP are ever probed. Asking a stdio server the same question means **launching an
/// arbitrary local command**, which is the very act SkillForge exists to let somebody avoid until they have looked; a
/// tool that ran it in order to inspect it would be arguing with itself. stdio servers are reported from their
/// declaration alone.
/// </remarks>
public interface IMcpProtocolAdapter
{
    /// <summary>Gets the protocol revision this adapter implements, for example <c>2026-07-28</c>.</summary>
    string ProtocolVersion { get; }

    /// <summary>
    /// Asks a server about itself.
    /// </summary>
    /// <param name="server">The declaration to probe. Must be an HTTP server.</param>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>
    /// What the server said, or why it did not answer. This does not throw for an unreachable server: being unable to
    /// reach one server is a fact about that server, not a reason to abandon the inventory.
    /// </returns>
    Task<McpServerProbe> ProbeAsync(McpServerDeclaration server, CancellationToken cancellationToken = default);
}
