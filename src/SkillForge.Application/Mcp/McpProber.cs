using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Runs the protocol adapters over the servers that can be probed.
/// </summary>
/// <remarks>
/// "Can be probed" means HTTP. A stdio server is recorded as <see cref="McpProbeStatus.NotProbed"/> with the reason
/// stated, rather than left out — a reader must be able to tell "did not answer" from "was never asked".
/// </remarks>
public sealed class McpProber
{
    /// <summary>
    /// Capabilities a server can declare that the specification has deprecated.
    /// </summary>
    /// <remarks>
    /// <c>logging</c> only. Roots and Sampling were deprecated by the same SEP and are frequently listed beside it, but
    /// they are <strong>client</strong> capabilities — a server cannot declare them, so looking for them here would be
    /// a check that can never fire. Verified against the deprecated-features registry for 2026-07-28.
    /// </remarks>
    private static readonly string[] DeprecatedServerCapabilities = ["logging"];

    private readonly IReadOnlyList<IMcpProtocolAdapter> _adapters;

    /// <summary>Initialises the prober.</summary>
    /// <param name="adapters">One adapter per protocol revision, newest first.</param>
    public McpProber(IEnumerable<IMcpProtocolAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = [.. adapters];
    }

    /// <summary>
    /// Probes every HTTP server in the list.
    /// </summary>
    /// <param name="servers">The declarations found by the migration inventory.</param>
    /// <param name="cancellationToken">Token used to cancel the probing.</param>
    /// <returns>One result per server, in the order given, plus what the results imply.</returns>
    public async Task<McpProbeOutcome> ProbeAsync(
        IReadOnlyList<McpServerDeclaration> servers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servers);

        var probes = new List<McpServerProbe>(servers.Count);
        var diagnostics = new List<Diagnostic>();

        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (server.Transport != McpTransport.Http)
            {
                probes.Add(McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.NotProbed,
                    "not an HTTP server; SkillForge never launches a local command to inspect it"));
                continue;
            }

            var adapter = _adapters.Count > 0 ? _adapters[0] : null;

            if (adapter is null)
            {
                probes.Add(McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.NotProbed,
                    "no protocol adapter is registered"));
                continue;
            }

            var probe = await adapter.ProbeAsync(server, cancellationToken).ConfigureAwait(false);

            probes.Add(probe);
            diagnostics.AddRange(Observations(probe, server));
        }

        return new McpProbeOutcome(probes, diagnostics);
    }

    private static IEnumerable<Diagnostic> Observations(McpServerProbe probe, McpServerDeclaration server)
    {
        if (probe.Status == McpProbeStatus.NoDiscovery)
        {
            yield return Diagnostic.Info(
                DiagnosticCodes.McpNoDiscovery,
                $"'{probe.ServerName}' does not implement server/discover, which 2026-07-28 made mandatory, so it is "
                    + "almost certainly a handshake-based revision (2025-11-25 or earlier).",
                server.SourcePath,
                suggestion: "Not a fault: the older revisions remain supported through backward compatibility. It "
                    + "does mean SkillForge cannot read its capabilities without an initialize handshake.");
        }

        foreach (var capability in probe.Capabilities
            .Where(capability => DeprecatedServerCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase)))
        {
            yield return Diagnostic.Info(
                DiagnosticCodes.McpDeprecatedCapability,
                $"'{probe.ServerName}' declares '{capability}', deprecated in 2026-07-28.",
                server.SourcePath,
                suggestion: "Deprecated features stay in the specification for at least twelve months. Logging's "
                    + "migration path is stderr for stdio, or OpenTelemetry for observability.");
        }
    }
}
