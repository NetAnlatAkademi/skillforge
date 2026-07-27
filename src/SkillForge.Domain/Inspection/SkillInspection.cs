using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Domain.Inspection;

/// <summary>
/// What a skill contains and what its contents imply it can do.
/// </summary>
/// <remarks>
/// Descriptive, never a verdict. The capabilities are inferred from what is in the directory, so they say
/// "this skill ships a script" rather than "this skill is dangerous" — SkillForge does not classify a skill
/// as safe or malicious.
/// </remarks>
/// <param name="SkillName">Declared skill name.</param>
/// <param name="SkillPath">Directory that was inspected.</param>
/// <param name="SkillVersion">Declared version, if any.</param>
/// <param name="Files">Every file in the skill, ordered by path.</param>
/// <param name="ExternalUrls">Distinct external URLs found in the skill's entry point, ordered.</param>
/// <param name="Capabilities">Capabilities the contents imply, ordered.</param>
/// <param name="DeclaredTools">Tools the frontmatter declares under <c>allowed-tools</c>.</param>
/// <param name="Diagnostics">Informational findings about the contents.</param>
public sealed record SkillInspection(
    string SkillName,
    string SkillPath,
    string? SkillVersion,
    IReadOnlyList<SkillResource> Files,
    IReadOnlyList<string> ExternalUrls,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> DeclaredTools,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Number of warnings among <see cref="Diagnostics"/>.</summary>
    public int Warnings => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>Number of errors among <see cref="Diagnostics"/>.</summary>
    public int Errors => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
}
