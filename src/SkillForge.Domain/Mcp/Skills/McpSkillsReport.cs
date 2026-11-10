using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// What <c>mcp skills inspect</c> or <c>mcp skills verify</c> found for one configuration file.
/// </summary>
/// <param name="Path">The configuration file that was read.</param>
/// <param name="Probed">Whether any server was asked anything. Nothing is, without <c>--probe</c>.</param>
/// <param name="Servers">One entry per declared server, in declaration order.</param>
/// <param name="Diagnostics">The findings, ordered.</param>
public sealed record McpSkillsReport(
    string Path,
    bool Probed,
    IReadOnlyList<McpSkillsServerReport> Servers,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Gets a value indicating whether any finding is about the integrity of served content.</summary>
    /// <remarks>
    /// The two informational codes — a dynamic skill and an incomplete listing — are statements about what could be
    /// checked, not about what was found, so they do not count.
    /// </remarks>
    public bool HasIntegrityFindings => Diagnostics.Any(finding => DiagnosticCodes.IsMcpSkillIntegrityFinding(finding.Code));
}
