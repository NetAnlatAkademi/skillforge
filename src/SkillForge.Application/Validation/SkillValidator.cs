using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Validation;

/// <summary>
/// Default <see cref="ISkillValidator"/>: runs every rule it was given and orders the results.
/// </summary>
/// <remarks>
/// A rule reporting an error does not stop the others — the user should see everything wrong with a
/// skill in one run, not fix one thing at a time.
/// </remarks>
public sealed class SkillValidator : ISkillValidator
{
    private readonly IReadOnlyList<ISkillValidationRule> _rules;

    /// <summary>Initialises the validator.</summary>
    /// <param name="rules">Rules to run. The order they are given in does not affect the output.</param>
    public SkillValidator(IEnumerable<ISkillValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
    }

    /// <summary>Gets the codes of the rules this validator will run.</summary>
    public IReadOnlyList<string> RuleCodes => _rules.Select(rule => rule.Code).ToArray();

    /// <inheritdoc />
    public async Task<ValidationReport> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<Diagnostic>();

        foreach (var rule in _rules)
        {
            // No try/catch: a rule that throws is a bug in that rule, and hiding it would leave the
            // user with a quietly incomplete report. It surfaces as an unexpected application failure.
            var findings = await rule.ValidateAsync(skill, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(findings);
        }

        return ValidationReport.For(skill, DiagnosticOrdering.Sort(diagnostics));
    }
}
