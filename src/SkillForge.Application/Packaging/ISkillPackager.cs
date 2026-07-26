using SkillForge.Domain;
using SkillForge.Domain.Packaging;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Packaging;

/// <summary>
/// Turns a loaded skill into a distributable package.
/// </summary>
public interface ISkillPackager
{
    /// <summary>
    /// Packages a skill.
    /// </summary>
    /// <param name="skill">Skill to package.</param>
    /// <param name="outputDirectory">Directory to write the archive, hash and manifest to.</param>
    /// <param name="versionOverride">
    /// Version to package as, or <see langword="null"/> to use the skill's declared version.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>What was produced, or a failure when the skill cannot be packaged.</returns>
    Task<OperationResult<SkillPackage>> PackAsync(
        SkillDefinition skill,
        string outputDirectory,
        string? versionOverride,
        CancellationToken cancellationToken);
}
