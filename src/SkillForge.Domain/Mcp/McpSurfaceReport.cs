using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Policy;

namespace SkillForge.Domain.Mcp;

/// <summary>
/// How much a server opens to an agent, and how much of that changes things.
/// </summary>
public enum SurfaceRisk
{
    /// <summary>A surface small enough that nothing about its size is worth saying.</summary>
    Informational,

    /// <summary>Above the configured warning threshold, or privileged without being large.</summary>
    Medium,

    /// <summary>Large and privileged, or above the configured high threshold.</summary>
    High,
}

/// <summary>
/// What one MCP server exposes.
/// </summary>
/// <remarks>
/// The counts come from the server's own <c>tools/list</c>, so they exist only when it was asked. A server that
/// was not probed carries <see cref="NotProbedReason"/> and zero counts, and the report says which — a zero that
/// means "nobody looked" must never read as "there is nothing there".
///
/// The categories are name-shaped: a tool called <c>delete_deployment</c> is counted as one that changes things.
/// That is a heuristic and is labelled as one, because the alternative — reading the description with a model —
/// would make a deterministic report depend on a model's mood.
/// </remarks>
/// <param name="ServerName">The name the configuration gives the server.</param>
/// <param name="ToolCount">How many tools it returned.</param>
/// <param name="InitiallyExposed">
/// How many were in the first response. Equal to <paramref name="ToolCount"/> for every server that does not
/// narrow what it lists, which is what makes the pair worth printing.
/// </param>
/// <param name="WriteCapableTools">Tools whose names say they change something.</param>
/// <param name="CredentialCapableTools">Tools whose names say they touch a secret.</param>
/// <param name="AdminTools">Tools whose names say they govern access.</param>
/// <param name="ProgressiveDiscoveryDetected">
/// Whether anything observed says the server narrows what it exposes as a task goes on. False means not detected,
/// never "not implemented": the mechanism is new and a server may do it in a way nothing here recognises.
/// </param>
/// <param name="NotProbedReason">Why there are no counts, or <see langword="null"/> when there are.</param>
/// <param name="Risk">The combined verdict.</param>
public sealed record McpToolSurface(
    string ServerName,
    int ToolCount,
    int InitiallyExposed,
    IReadOnlyList<string> WriteCapableTools,
    IReadOnlyList<string> CredentialCapableTools,
    IReadOnlyList<string> AdminTools,
    bool ProgressiveDiscoveryDetected,
    string? NotProbedReason,
    SurfaceRisk Risk)
{
    /// <summary>Gets the tools that change, govern or reach a secret, without double-counting any.</summary>
    public IReadOnlyList<string> PrivilegedTools =>
    [
        .. WriteCapableTools
            .Concat(CredentialCapableTools)
            .Concat(AdminTools)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];
}

/// <summary>
/// What <c>mcp surface</c> found in one configuration.
/// </summary>
/// <param name="Path">The file that was read.</param>
/// <param name="Servers">Each declared server's surface, ordered by name.</param>
/// <param name="Thresholds">The thresholds the counts were judged against.</param>
/// <param name="Diagnostics">The findings, in the standard report order.</param>
public sealed record McpSurfaceReport(
    string Path,
    IReadOnlyList<McpToolSurface> Servers,
    McpSurfaceThresholds Thresholds,
    IReadOnlyList<Diagnostic> Diagnostics);
