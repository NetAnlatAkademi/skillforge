using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Validation;

/// <summary>
/// Counts of what a validation run found.
/// </summary>
/// <param name="Errors">Number of <see cref="DiagnosticSeverity.Error"/> diagnostics.</param>
/// <param name="Warnings">Number of <see cref="DiagnosticSeverity.Warning"/> diagnostics.</param>
/// <param name="Info">Number of <see cref="DiagnosticSeverity.Info"/> diagnostics.</param>
public sealed record ValidationSummary(int Errors, int Warnings, int Info)
{
    /// <summary>
    /// Gets a value indicating whether the skill is usable: no errors were found.
    /// </summary>
    /// <remarks>
    /// Warnings do not make a skill invalid. Whether they should fail a build is the caller's decision,
    /// expressed through strict mode, not a property of the skill.
    /// </remarks>
    public bool IsValid => Errors == 0;

    /// <summary>Gets the total number of diagnostics.</summary>
    public int Total => Errors + Warnings + Info;

    /// <summary>Counts the diagnostics by severity.</summary>
    /// <param name="diagnostics">Diagnostics to count.</param>
    /// <returns>The summary.</returns>
    public static ValidationSummary FromDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var errors = 0;
        var warnings = 0;
        var info = 0;

        foreach (var diagnostic in diagnostics)
        {
            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Error:
                    errors++;
                    break;
                case DiagnosticSeverity.Warning:
                    warnings++;
                    break;
                default:
                    info++;
                    break;
            }
        }

        return new ValidationSummary(errors, warnings, info);
    }
}
