using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation;

/// <summary>
/// One validation rule, owning one diagnostic code.
/// </summary>
/// <remarks>
/// Rules are independent: each one is unit testable on its own, none of them read the file system
/// directly, and none of them know about the others. A rule that finds nothing returns an empty list —
/// it never throws to signal a finding. An exception from a rule is a bug in that rule and is allowed to
/// surface as an unexpected application failure rather than being swallowed.
/// </remarks>
public interface ISkillValidationRule
{
    /// <summary>Gets the diagnostic code this rule reports.</summary>
    string Code { get; }

    /// <summary>
    /// Examines a loaded skill.
    /// </summary>
    /// <param name="skill">The skill to examine.</param>
    /// <param name="cancellationToken">Token used to cancel the rule.</param>
    /// <returns>The findings, or an empty list when the rule has nothing to report.</returns>
    ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken);
}
