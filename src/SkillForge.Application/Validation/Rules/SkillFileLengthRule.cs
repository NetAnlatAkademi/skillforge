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
///
/// **Raised from 500 to 1000 on 2026-07-29**, at the operator's request. The measurement supports it: at 500 the rule
/// spoke about 33 skills in the 229-skill corpus, and inspecting the longest of them showed instructions that were long
/// because the job is long, not because reference material was in the wrong place. A threshold that fires on a seventh
/// of real input is the SF1009 shape. At 1000 it speaks only about entry points that are genuinely unusual.
/// </remarks>
public sealed class SkillFileLengthRule : ISkillValidationRule
{
    private const int MaximumRecommendedLines = 1000;

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
