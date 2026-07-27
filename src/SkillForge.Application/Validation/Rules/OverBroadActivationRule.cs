using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports a description that claims the skill applies always, or to everything.
/// </summary>
/// <remarks>
/// The mirror image of SF1002. That rule asks whether the description says *when* the skill applies; this one asks
/// whether it says "whenever", which is the same failure wearing confidence. An agent choosing between skills needs
/// a scope it can match against a task, and "everything" is not one.
///
/// Only the description is examined. The body can reasonably say "always run the tests" — that is instruction, not
/// activation.
/// </remarks>
public sealed class OverBroadActivationRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ActivationTooBroad;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (string.IsNullOrWhiteSpace(skill.Description))
        {
            return RuleResult.None();
        }

        var diagnostics = ActivationRiskPatterns.TooBroad
            .Where(pattern => pattern.Pattern.IsMatch(skill.Description))
            .Select(pattern => Diagnostic.Warning(
                Code,
                $"The description claims the skill applies broadly ({pattern.Name}).",
                SkillDefinition.SkillFileName,
                skill.Frontmatter.StartLine,
                $"Name the situation the skill is for instead: {pattern.Why}."))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
