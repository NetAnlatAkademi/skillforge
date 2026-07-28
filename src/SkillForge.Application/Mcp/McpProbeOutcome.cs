using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;

namespace SkillForge.Application.Mcp;

/// <summary>
/// What probing produced: one result per server, and the observations they imply.
/// </summary>
/// <param name="Probes">One entry per declared server, in declaration order.</param>
/// <param name="Diagnostics">SF8004 and SF8005 findings, informational like the rest of the band.</param>
public sealed record McpProbeOutcome(
    IReadOnlyList<McpServerProbe> Probes,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Nothing was probed.</summary>
    public static McpProbeOutcome None { get; } = new([], []);
}
