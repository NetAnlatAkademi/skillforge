namespace SkillForge.Application.Abstractions;

/// <summary>
/// What <c>init</c> created.
/// </summary>
/// <param name="DirectoryPath">Absolute path of the new skill directory.</param>
/// <param name="CreatedFiles">Absolute paths of the files written, ordered.</param>
public sealed record SkillInitializationResult(
    string DirectoryPath,
    IReadOnlyList<string> CreatedFiles);
