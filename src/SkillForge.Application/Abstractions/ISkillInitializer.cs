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
        CancellationToken cancellationToken);
}

/// <summary>
/// What to put in a generated skill.
/// </summary>
/// <param name="Name">Skill name.</param>
/// <param name="Description">Description, or <see langword="null"/> for a placeholder.</param>
/// <param name="Author">Author recorded in metadata, or <see langword="null"/> to omit it.</param>
/// <param name="License">SPDX licence identifier.</param>
/// <param name="Version">Initial version.</param>
/// <param name="Force">Whether writing into an existing directory is acceptable.</param>
public sealed record SkillInitializationOptions(
    string Name,
    string? Description = null,
    string? Author = null,
    string License = "MIT",
    string Version = "0.1.0",
    bool Force = false);

/// <summary>
/// What <c>init</c> created.
/// </summary>
/// <param name="DirectoryPath">Absolute path of the new skill directory.</param>
/// <param name="CreatedFiles">Absolute paths of the files written, ordered.</param>
public sealed record SkillInitializationResult(
    string DirectoryPath,
    IReadOnlyList<string> CreatedFiles);
