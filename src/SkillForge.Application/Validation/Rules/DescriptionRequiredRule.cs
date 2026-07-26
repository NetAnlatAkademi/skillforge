using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the skill declares a description.
/// </summary>
/// <remarks>
/// The description is how an agent decides whether to activate the skill at all, so a skill without one
/// is unusable in practice.
/// </remarks>
public sealed class DescriptionRequiredRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.DescriptionMissing;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        IReadOnlyList<Diagnostic> diagnostics = string.IsNullOrWhiteSpace(skill.Description)
            ?
            [
                Diagnostic.Error(
                    Code,
                    "The skill does not declare a description.",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "Add a 'description' field saying what the skill does and when it applies."),
            ]
            : [];

        return ValueTask.FromResult(diagnostics);
    }
}
