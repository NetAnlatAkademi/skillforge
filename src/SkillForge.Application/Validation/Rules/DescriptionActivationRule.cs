using System.Text.RegularExpressions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the description says when the skill applies.
/// </summary>
/// <remarks>
/// This is a heuristic and it is honest about being one: it looks for the words people actually use to
/// describe a trigger — <c>when</c>, <c>while</c>, <c>during</c>, <c>before</c>, <c>after</c>, <c>if</c>.
/// A description can state its activation context without any of them, which is why this is a warning
/// the author can reasonably ignore rather than an error.
/// </remarks>
public sealed partial class DescriptionActivationRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.DescriptionWithoutActivationContext;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        // A missing description belongs to SF0005.
        if (string.IsNullOrWhiteSpace(skill.Description))
        {
            return ValueTask.FromResult<IReadOnlyList<Diagnostic>>([]);
        }

        IReadOnlyList<Diagnostic> diagnostics = ActivationCuePattern().IsMatch(skill.Description)
            ? []
            :
            [
                Diagnostic.Warning(
                    Code,
                    "The description does not say when this skill should be used.",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "Name the situation that should trigger the skill — for example "
                        + "'Use this skill when reviewing an ASP.NET Core API'."),
            ];

        return ValueTask.FromResult(diagnostics);
    }

    [GeneratedRegex(
        @"\b(when|whenever|while|during|before|after|if)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActivationCuePattern();
}
