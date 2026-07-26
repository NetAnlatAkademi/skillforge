using SkillForge.Domain;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Reads a skill from disk into a <see cref="SkillDefinition"/>.
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// Loads the skill at the given location.
    /// </summary>
    /// <param name="path">
    /// Either the skill directory or the <c>SKILL.md</c> file itself. Relative paths are resolved
    /// against the current working directory.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    /// <returns>
    /// The loaded skill, or a failure when the skill cannot be modelled at all — a missing
    /// <c>SKILL.md</c>, a missing frontmatter block, or YAML that cannot be parsed.
    /// </returns>
    Task<OperationResult<SkillDefinition>> LoadAsync(string path, CancellationToken cancellationToken);
}
