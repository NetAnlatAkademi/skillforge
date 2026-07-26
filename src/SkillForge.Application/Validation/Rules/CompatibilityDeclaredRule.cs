using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that agent compatibility is declared.
/// </summary>
/// <remarks>
/// Skills are written against a particular agent's conventions. Saying which ones were actually tried
/// saves the next person from discovering it by trial. A warning, since an undeclared skill still runs.
/// </remarks>
public sealed class CompatibilityDeclaredRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.CompatibilityMissing;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        IReadOnlyList<Diagnostic> diagnostics = skill.Frontmatter.Compatibility.Count == 0
            ?
            [
                Diagnostic.Warning(
                    Code,
                    "No agent compatibility is declared.",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "List the agents this skill was written for under 'compatibility', "
                        + "for example 'claude-code'."),
            ]
            : [];

        return ValueTask.FromResult(diagnostics);
    }
}
