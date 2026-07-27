using SkillForge.Application.Skills;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the skill name is a usable identifier.
/// </summary>
/// <remarks>
/// The definition lives in <see cref="SkillName"/> so that <c>init</c> and this rule cannot disagree: a
/// name <c>init</c> generates is by construction one this rule accepts.
/// </remarks>
public sealed class NameFormatRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.NameInvalid;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        // A missing name belongs to SF0004; DescribeProblem stays quiet about it for that reason.
        var reason = SkillName.DescribeProblem(skill.Name);

        return reason is null
            ? RuleResult.None()
            : RuleResult.One(Diagnostic.Error(
                Code,
                $"The skill name '{skill.Name}' is not valid: {reason}",
                SkillDefinition.SkillFileName,
                skill.Frontmatter.StartLine,
                "Use lowercase letters, digits and single hyphens, starting with a letter — "
                    + "for example 'dotnet-api-review'."));
    }
}
