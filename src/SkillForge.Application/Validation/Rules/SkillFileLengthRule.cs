using System.Globalization;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that <c>SKILL.md</c> is short enough to stay readable.
/// </summary>
/// <remarks>
/// A long entry point is a sign that reference material should move into its own file, which the agent
/// can then read only when it needs to. The limit is a warning: some skills genuinely are long.
/// </remarks>
public sealed class SkillFileLengthRule : ISkillValidationRule
{
    private const int MaximumRecommendedLines = 500;

    /// <inheritdoc />
    public string Code => DiagnosticCodes.SkillFileTooLong;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        return skill.SkillFileLineCount <= MaximumRecommendedLines
            ? RuleResult.None()
            : RuleResult.One(Diagnostic.Warning(
                Code,
                SkillDefinition.SkillFileName
                    + " has "
                    + skill.SkillFileLineCount.ToString(CultureInfo.InvariantCulture)
                    + " lines, more than the recommended "
                    + MaximumRecommendedLines.ToString(CultureInfo.InvariantCulture)
                    + ".",
                SkillDefinition.SkillFileName,
                suggestion: "Move reference material into files under 'references/' and link to them, "
                    + "so the agent reads them only when it needs them."));
    }
}
