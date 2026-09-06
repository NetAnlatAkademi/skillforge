using SkillForge.Application.Mcp;
using SkillForge.Domain.Identity;

namespace SkillForge.Application.Identity;

/// <summary>
/// Reads the identities one MCP configuration file uses.
/// </summary>
/// <remarks>
/// A thin orchestration on purpose: <see cref="McpFileInspector"/> already knows how to read a configuration and,
/// when asked, how to ask each HTTP server about itself. This adds the identity reading on top, so a file is
/// parsed once and a stdio server is still never launched.
/// </remarks>
public sealed class McpIdentityInspector
{
    private readonly McpFileInspector _inspector;

    /// <summary>Initialises the inspector.</summary>
    /// <param name="inspector">Reads the configuration and probes the HTTP servers when asked.</param>
    public McpIdentityInspector(McpFileInspector inspector)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        _inspector = inspector;
    }

    /// <summary>Reads what identity each declared server is reached with.</summary>
    /// <param name="path">Path of the configuration file.</param>
    /// <param name="probe">
    /// Whether to ask each HTTP server about itself. A server's <c>401</c> is the only place a scope or an issuer
    /// can be observed, so without this the report describes what the file says and nothing more.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The identities, ordered by server name.</returns>
    public async Task<McpIdentityReport> InspectAsync(
        string path,
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var inspection = await _inspector.InspectAsync(path, probe, cancellationToken).ConfigureAwait(false);

        var identities = inspection.Servers
            .Select(server => IdentityInference.From(
                server,
                inspection.Probes.FirstOrDefault(candidate =>
                    string.Equals(candidate.ServerName, server.Name, StringComparison.Ordinal))))
            .OrderBy(server => server.ServerName, StringComparer.Ordinal)
            .ToArray();

        return new McpIdentityReport(path, identities, inspection.Diagnostics);
    }
}
