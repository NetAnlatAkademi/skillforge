using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports activation text that tries to win activation rather than describe it.
/// </summary>
/// <remarks>
/// A skill saying when it applies has no reason to tell an agent what to disregard, which skill not to use, or what
/// to do before every response. Text like that is aimed at the agent's decision rather than a reader's
/// understanding.
///
/// **The description only, and that was learned the hard way.** This rule scanned the body too. Measured on 229 real
/// skills that produced twelve findings of which roughly one was real: the rest were ordinary English in ordinary
/// prose — "say so instead of hiding behind tooling", "# Ignore other fields", and, memorably, a security skill's own
/// detection pattern written as a string literal. A body is instructions; a description is activation text. Finding
/// injected instructions inside a body is a different problem, and it has its own reserved band (SF4xxx) rather than
/// being approximated here at a 90% false-positive rate.
///
/// A warning, and the message says what was recognised rather than what it means. SkillForge does not conclude that
/// a skill is malicious (ADR-006) — a legitimate skill can be written clumsily, and a reader with the finding in
/// front of them judges better than a regex.
/// </remarks>
public sealed class ActivationManipulationRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ActivationManipulation;

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

        var diagnostics = ActivationRiskPatterns.Manipulation
            .Where(pattern => pattern.Pattern.IsMatch(skill.Description))
            .Select(pattern => Diagnostic.Warning(
                Code,
                $"The description contains {pattern.Name}.",
                SkillDefinition.SkillFileName,
                skill.Frontmatter.StartLine,
                $"Describe when the skill applies and leave the choice to the agent: {pattern.Why}. "
                    + "SkillForge is pointing this out, not calling the skill malicious."))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
