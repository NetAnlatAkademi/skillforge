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

            if (_adapters.Count == 0)
            {
                probes.Add(McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.NotProbed,
                    "no protocol adapter is registered"));
                continue;
            }

            var probe = await ProbeWithFallbackAsync(server, cancellationToken).ConfigureAwait(false);

            probes.Add(probe);
            diagnostics.AddRange(Observations(probe, server));
        }

        return new McpProbeOutcome(probes, diagnostics);
    }

    /// <summary>
    /// Tries each adapter in turn, newest revision first, and stops at the first that gets somewhere.
    /// </summary>
    /// <remarks>
    /// Only a "no discovery" answer is worth falling back on: it means the server is reachable and speaks an older
    /// dialect. An unreachable host or a 401 is the same answer whichever revision asks, so retrying it would double the
    /// requests to say the same thing. The SF8004 note is still emitted for the revision mismatch — it is a true and
    /// useful observation even once the fallback has read the server's details.
    /// </remarks>
    private async Task<McpServerProbe> ProbeWithFallbackAsync(
        McpServerDeclaration server,
        CancellationToken cancellationToken)
    {
        McpServerProbe? first = null;

        foreach (var adapter in _adapters)
        {
            var probe = await adapter.ProbeAsync(server, cancellationToken).ConfigureAwait(false);

            first ??= probe;

            if (probe.Status != McpProbeStatus.NoDiscovery)
            {
                // Carry the older probe's answer, but keep the fact that discovery was missing: both are true.
                return first.Status == McpProbeStatus.NoDiscovery && probe.Status == McpProbeStatus.Answered
                    ? probe with { Detail = first.Detail }
                    : probe;
            }
        }

        return first!;
    }

    private static IEnumerable<Diagnostic> Observations(McpServerProbe probe, McpServerDeclaration server)
    {
        // Emitted whenever discovery was missing, including when an older adapter went on to answer: the server still
        // does not implement a method 2026-07-28 makes mandatory.
        if (probe.Status == McpProbeStatus.NoDiscovery
            || (probe.Detail is not null && probe.AnsweredRevision is not null and not "2026-07-28"))
        {
            yield return Diagnostic.Info(
                DiagnosticCodes.McpNoDiscovery,
                $"'{probe.ServerName}' does not implement server/discover, which 2026-07-28 made mandatory, so it is "
                    + "almost certainly a handshake-based revision (2025-11-25 or earlier).",
                server.SourcePath,
                suggestion: "Not a fault: the older revisions remain supported through backward compatibility. It "
                    + "does mean SkillForge cannot read its capabilities without an initialize handshake.");
        }

        // Only the missing-metadata case is a finding, and a code means one thing: a server that names its Protected
        // Resource Metadata is behaving correctly, and that is reported in the probe section rather than as an
        // observation. A server that requires authorization and points nowhere leaves a conforming client stuck.
        if (probe is { Status: McpProbeStatus.RequiresAuthorization, Authorization.ResourceMetadataUrl: null })
        {
            yield return Diagnostic.Info(
                DiagnosticCodes.McpAuthorizationWithoutMetadata,
                $"'{probe.ServerName}' requires authorization but its challenge names no resource_metadata.",
                server.SourcePath,
                suggestion: "MCP servers must implement OAuth 2.0 Protected Resource Metadata (RFC 9728), and clients "
                    + "must use it to discover the authorization server. Without resource_metadata in the challenge, a "
                    + "conforming client has nowhere to look.");
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

        foreach (var finding in McpToolConformance.Check(probe.ServerName, server.SourcePath, probe.ToolsOrEmpty))
        {
            yield return finding;
        }
    }
}
