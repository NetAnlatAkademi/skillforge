using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Measures how much of an agent's attention a server takes up before it has done anything.
/// </summary>
/// <remarks>
/// Every tool a server exposes is in the model's context whether or not the task needs it, so a server with a
/// hundred and forty-seven tools is a decision about every conversation the agent has — and it is usually a
/// decision nobody made, because the number arrives one tool at a time.
///
/// **No model is used here, and no description is read.** The categories come from tool names, matched against a
/// list that is in this file where it can be read and argued with. Asking a model whether a tool is dangerous
/// would make the same configuration produce different reports on different days, which is not a check anybody
/// can put in a pipeline.
/// </remarks>
public static class McpSurfaceAnalyzer
{
    /// <summary>Name fragments that say a tool changes something.</summary>
    private static readonly string[] WriteWords =
    [
        "write", "create", "update", "delete", "remove", "set_", "put_", "patch", "insert", "upsert",
        "deploy", "publish", "push", "execute", "run_", "restart", "stop_", "start_", "scale", "apply",
        "send", "post_", "merge", "revert", "rollback", "drop",
    ];

    /// <summary>Name fragments that say a tool touches a secret.</summary>
    private static readonly string[] CredentialWords =
        ["secret", "credential", "token", "password", "apikey", "api_key", "keyvault", "vault", "certificate"];

    /// <summary>Name fragments that say a tool governs access.</summary>
    private static readonly string[] AdminWords =
        ["admin", "grant", "revoke", "permission", "policy", "role", "owner", "member", "acl", "sudo", "root"];

    /// <summary>
    /// Name fragments of a tool whose job is to hand out other tools — the shape progressive discovery takes when
    /// a server implements it with a meta-tool rather than with a capability.
    /// </summary>
    private static readonly string[] DiscoveryToolWords =
        ["search_tools", "list_tools", "find_tools", "discover_tools", "get_tools", "tool_search"];

    /// <summary>Capability name fragments that say the server narrows what it exposes.</summary>
    private static readonly string[] DiscoveryCapabilityWords =
        ["discover", "progressive", "toolfilter", "toolset"];

    /// <summary>Measures every declared server and reports what the numbers mean.</summary>
    /// <param name="path">The configuration file the servers were read from.</param>
    /// <param name="servers">The declarations.</param>
    /// <param name="probes">What each probed server said. Empty when nothing was probed.</param>
    /// <param name="thresholds">Counts to judge against.</param>
    /// <param name="readingDiagnostics">Findings from reading the file, carried through unchanged.</param>
    /// <returns>The report, servers ordered by name and findings in the standard order.</returns>
    public static McpSurfaceReport Analyze(
        string path,
        IReadOnlyList<McpServerDeclaration> servers,
        IReadOnlyList<McpServerProbe> probes,
        McpSurfaceThresholds thresholds,
        IReadOnlyList<Diagnostic> readingDiagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(readingDiagnostics);

        var surfaces = servers
            .Select(server => Measure(
                server,
                probes.FirstOrDefault(probe =>
                    string.Equals(probe.ServerName, server.Name, StringComparison.Ordinal)),
                thresholds))
            .OrderBy(surface => surface.ServerName, StringComparer.Ordinal)
            .ToArray();

        var findings = new List<Diagnostic>(readingDiagnostics);

        foreach (var surface in surfaces)
        {
            AddFindings(surface, thresholds, path, findings);
        }

        return new McpSurfaceReport(path, surfaces, thresholds, DiagnosticOrdering.Sort(findings));
    }

    private static McpToolSurface Measure(
        McpServerDeclaration server,
        McpServerProbe? probe,
        McpSurfaceThresholds thresholds)
    {
        if (probe is not { Status: McpProbeStatus.Answered })
        {
            return new McpToolSurface(
                server.Name,
                0,
                0,
                [],
                [],
                [],
                false,
                probe?.Detail ?? NotAskedReason(server, probe),
                SurfaceRisk.Informational);
        }

        var names = probe.ToolsOrEmpty.Select(tool => tool.Name).ToArray();

        var write = Matching(names, WriteWords);
        var credential = Matching(names, CredentialWords);
        var admin = Matching(names, AdminWords);

        var progressive = names.Any(name => Contains(name, DiscoveryToolWords))
            || probe.Capabilities.Any(capability => Contains(capability, DiscoveryCapabilityWords));

        var privileged = write.Count + credential.Count + admin.Count > 0;

        return new McpToolSurface(
            server.Name,
            names.Length,
            names.Length,
            write,
            credential,
            admin,
            progressive,
            null,
            RiskOf(names.Length, privileged, thresholds));
    }

    /// <summary>
    /// The size and the privilege together. A large surface of read-only tools costs context; a small surface that
    /// can delete things costs something else; both at once is the case worth interrupting somebody for.
    /// </summary>
    private static SurfaceRisk RiskOf(int toolCount, bool privileged, McpSurfaceThresholds thresholds)
    {
        if (toolCount >= thresholds.HighToolCount || (toolCount >= thresholds.WarningToolCount && privileged))
        {
            return SurfaceRisk.High;
        }

        return toolCount >= thresholds.WarningToolCount ? SurfaceRisk.Medium : SurfaceRisk.Informational;
    }

    private static void AddFindings(
        McpToolSurface surface,
        McpSurfaceThresholds thresholds,
        string path,
        List<Diagnostic> findings)
    {
        if (surface.NotProbedReason is not null || surface.ToolCount < thresholds.WarningToolCount)
        {
            return;
        }

        findings.Add(Diagnostic.Warning(
            DiagnosticCodes.McpToolSurfaceLarge,
            $"'{surface.ServerName}' exposes {surface.ToolCount} tools, at or above the configured threshold of "
                + $"{thresholds.WarningToolCount}. All of them are in the agent's context whether the task needs "
                + "them or not.",
            path,
            suggestion: "Split the server, narrow what it registers, or raise the threshold deliberately under "
                + "'mcp.surface' in the policy file."));

        if (surface.PrivilegedTools is { Count: > 0 } privileged)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.McpPrivilegedToolsExposed,
                $"'{surface.ServerName}' exposes {privileged.Count} tool(s) whose names say they change things, "
                    + $"govern access or reach a secret, from the first response: {Sample(privileged)}.",
                path,
                suggestion: "Confirm an agent should be able to reach these without a further decision. The "
                    + "categories come from tool names, so check the list rather than trusting the count."));
        }

        if (!surface.ProgressiveDiscoveryDetected)
        {
            findings.Add(Diagnostic.Info(
                DiagnosticCodes.McpNoProgressiveDiscovery,
                $"Nothing observed says '{surface.ServerName}' narrows what it exposes as a task goes on, so the "
                    + "whole surface is in play from the start.",
                path,
                suggestion: "Information, not a defect: progressive discovery is new and few servers implement "
                    + "it. Reported because it is what makes a large surface unavoidable."));
        }
    }

    /// <summary>
    /// Why a server has no counts. A stdio server is never launched, so it has none by design rather than by
    /// failure, and saying that plainly is the difference between a gap and a bug report.
    /// </summary>
    private static string NotAskedReason(McpServerDeclaration server, McpServerProbe? probe) =>
        probe is null
            ? server.Transport == McpTransport.Stdio
                ? "not asked: SkillForge never launches a local server to inspect it"
                : "not asked: re-run with --probe to count what it exposes"
            : $"no tool list: {probe.Status}";

    private static IReadOnlyList<string> Matching(IReadOnlyList<string> names, IReadOnlyList<string> words) =>
    [
        .. names
            .Where(name => Contains(name, words))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    private static bool Contains(string value, IReadOnlyList<string> words) =>
        words.Any(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>Names a few of them rather than all: a message is read, a list is scrolled past.</summary>
    private static string Sample(IReadOnlyList<string> tools) =>
        tools.Count <= 5
            ? string.Join(", ", tools)
            : $"{string.Join(", ", tools.Take(5))} and {tools.Count - 5} more";
}
