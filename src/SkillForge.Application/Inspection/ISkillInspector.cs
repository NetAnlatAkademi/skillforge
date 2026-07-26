using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Inspection;

/// <summary>
/// Summarises a loaded skill's contents and behaviour surface.
/// </summary>
public interface ISkillInspector
{
    /// <summary>Inspects a skill.</summary>
    /// <param name="skill">Skill to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>What the skill contains.</returns>
    ValueTask<SkillInspection> InspectAsync(SkillDefinition skill, CancellationToken cancellationToken);
}
