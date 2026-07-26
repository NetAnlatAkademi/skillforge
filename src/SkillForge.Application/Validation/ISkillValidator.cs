using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Validation;

/// <summary>
/// Runs every validation rule over a loaded skill and collects the findings.
/// </summary>
public interface ISkillValidator
{
    /// <summary>
    /// Validates a loaded skill.
    /// </summary>
    /// <param name="skill">The skill to validate.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns>
    /// A report whose diagnostics are ordered deterministically, so that identical input produces
    /// identical output.
    /// </returns>
    Task<ValidationReport> ValidateAsync(SkillDefinition skill, CancellationToken cancellationToken);
}
