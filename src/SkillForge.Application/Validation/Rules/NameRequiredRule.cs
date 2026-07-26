using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the skill declares a name.
/// </summary>
/// <remarks>
/// Without a name an agent has nothing to refer to the skill by, so this is an error rather than a
/// warning.
/// </remarks>
public sealed class NameRequiredRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.NameMissing;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        IReadOnlyList<Diagnostic> diagnostics = string.IsNullOrWhiteSpace(skill.Name)
            ?
            [
                Diagnostic.Error(
                    Code,
                    "The skill does not declare a name.",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "Add a 'name' field to the frontmatter, matching the skill's directory name."),
            ]
            : [];

        return ValueTask.FromResult(diagnostics);
    }
}
