using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Domain.Validation;

/// <summary>
/// The outcome of validating one skill: everything a report renderer or CI step needs.
/// </summary>
/// <param name="SkillName">
/// Name of the validated skill, or an empty string when the skill did not declare one.
/// </param>
/// <param name="SkillPath">Path that was validated.</param>
/// <param name="Diagnostics">
/// Every finding, in a deterministic order: most severe first, then by code, file and line.
/// </param>
/// <param name="Summary">Counts by severity.</param>
public sealed record ValidationReport(
    string SkillName,
    string SkillPath,
    IReadOnlyList<Diagnostic> Diagnostics,
    ValidationSummary Summary)
{
    /// <summary>
    /// Gets a value indicating whether the skill is usable, ignoring warnings.
    /// </summary>
    public bool IsValid => Summary.IsValid;

    /// <summary>
    /// Determines whether this run should be treated as a failure.
    /// </summary>
    /// <param name="strict">When <see langword="true"/>, warnings fail as well as errors.</param>
    /// <returns><see langword="true"/> when the caller should report failure.</returns>
    public bool HasFailed(bool strict) =>
        Summary.Errors > 0 || (strict && Summary.Warnings > 0);

    /// <summary>
    /// Creates a report for a skill that could not be loaded at all.
    /// </summary>
    /// <param name="skillPath">Path that was validated.</param>
    /// <param name="diagnostics">Findings explaining why the skill could not be loaded.</param>
    /// <returns>A report with no skill name and the given diagnostics.</returns>
    public static ValidationReport ForUnloadableSkill(
        string skillPath,
        IReadOnlyList<Diagnostic> diagnostics) =>
        new(string.Empty, skillPath, diagnostics, ValidationSummary.FromDiagnostics(diagnostics));

    /// <summary>
    /// Creates a report for a skill that loaded.
    /// </summary>
    /// <param name="skill">The validated skill.</param>
    /// <param name="diagnostics">Findings, already ordered.</param>
    /// <returns>The report.</returns>
    public static ValidationReport For(SkillDefinition skill, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(skill);

        return new ValidationReport(
            skill.Name,
            skill.DirectoryPath,
            diagnostics,
            ValidationSummary.FromDiagnostics(diagnostics));
    }
}
