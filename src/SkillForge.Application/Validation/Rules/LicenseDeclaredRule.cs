using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that a license is declared.
/// </summary>
/// <remarks>
/// Anyone deciding whether they may use a skill needs to know its terms. This is a warning: an
/// unlicensed skill still works, it just cannot be safely adopted.
/// </remarks>
public sealed class LicenseDeclaredRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.LicenseMissing;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        IReadOnlyList<Diagnostic> diagnostics = string.IsNullOrWhiteSpace(skill.Frontmatter.License)
            ?
            [
                Diagnostic.Warning(
                    Code,
                    "No license is declared.",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "Add a 'license' field, for example 'license: MIT', so others know the terms."),
            ]
            : [];

        return ValueTask.FromResult(diagnostics);
    }
}
