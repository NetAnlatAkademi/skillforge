namespace SkillForge.Application.Abstractions;

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
