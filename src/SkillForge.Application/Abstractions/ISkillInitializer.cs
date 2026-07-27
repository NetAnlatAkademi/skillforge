using SkillForge.Domain;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Creates a new skill directory from a template.
/// </summary>
public interface ISkillInitializer
{
    /// <summary>
    /// Creates the skill.
    /// </summary>
    /// <param name="targetDirectory">Directory to create the skill in.</param>
    /// <param name="options">What to put in the generated files.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>What was created, or a failure when the name cannot be used.</returns>
    Task<OperationResult<SkillInitializationResult>> InitializeAsync(
        string targetDirectory,
        SkillInitializationOptions options,
        CancellationToken cancellationToken = default);
}
