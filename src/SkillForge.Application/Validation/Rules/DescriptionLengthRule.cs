using System.Globalization;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the description is long enough to be useful.
/// </summary>
/// <remarks>
/// A threshold on characters is a blunt instrument, and it is chosen deliberately: an agent picking
/// between skills has only the description to go on, and "Reviews APIs." does not distinguish this skill
/// from ten others. This is a warning, not an error — the skill still works.
/// </remarks>
public sealed class DescriptionLengthRule : ISkillValidationRule
{
    private const int MinimumUsefulLength = 40;

    /// <inheritdoc />
    public string Code => DiagnosticCodes.DescriptionTooShort;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        // A missing description belongs to SF0005.
        if (string.IsNullOrWhiteSpace(skill.Description))
        {
            return RuleResult.None();
        }

        var length = skill.Description.Trim().Length;
        return length >= MinimumUsefulLength
            ? RuleResult.None()
            : RuleResult.One(Diagnostic.Warning(
                Code,
                "The description is "
                    + length.ToString(CultureInfo.InvariantCulture)
                    + " characters long, which is unlikely to tell an agent when to use this skill.",
                SkillDefinition.SkillFileName,
                skill.Frontmatter.StartLine,
                "Describe what the skill does and the situation it applies to, in at least "
                    + MinimumUsefulLength.ToString(CultureInfo.InvariantCulture)
                    + " characters."));
    }
}
