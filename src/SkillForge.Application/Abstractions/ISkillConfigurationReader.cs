using SkillForge.Domain;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Reads a skill's optional <c>skillforge.yaml</c>.
/// </summary>
public interface ISkillConfigurationReader
{
    /// <summary>
    /// Reads the configuration for a skill.
    /// </summary>
    /// <param name="skillDirectory">Directory of the skill.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The configuration, or <see cref="SkillConfiguration.Default"/> when the file is absent. A file that
    /// exists but cannot be parsed yields the defaults <em>and</em> a diagnostic — silently ignoring a
    /// configuration file the user wrote would be worse than either honouring or rejecting it.
    /// </returns>
    Task<OperationResult<SkillConfiguration>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default);
}
