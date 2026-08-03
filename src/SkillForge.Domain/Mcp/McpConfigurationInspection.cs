using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Domain.Mcp;

/// <summary>
/// What one MCP configuration file declares, and what that reveals.
/// </summary>
/// <param name="Path">The file that was read.</param>
/// <param name="Servers">Servers the file declares, in the order they were read.</param>
/// <param name="Diagnostics">
/// What the declarations reveal, plus <c>SF1015</c> when the file itself could not be read — an inventory with a
/// silent gap in it looks like a configuration that declares nothing.
/// </param>
/// <param name="Probes">
/// What each server said about itself, when probing was asked for. Empty when it was not: a server that was never
/// asked must not be reported as one that did not answer.
/// </param>
public sealed record McpConfigurationInspection(
    string Path,
    IReadOnlyList<McpServerDeclaration> Servers,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<McpServerProbe> Probes)
{
    /// <summary>Gets a value indicating whether anything at all was found to report.</summary>
    public bool HasFindings => Diagnostics.Count > 0;
}
