namespace SkillForge.Application.Skills;

/// <summary>
/// What <c>init</c> should put in a new skill.
/// </summary>
/// <param name="Name">Skill name. Must satisfy <see cref="SkillName"/>.</param>
/// <param name="Description">Description, or <see langword="null"/> to use a placeholder.</param>
/// <param name="Author">Author recorded in metadata, or <see langword="null"/> to omit it.</param>
/// <param name="License">SPDX licence identifier.</param>
/// <param name="Version">Initial version.</param>
public sealed record SkillTemplateOptions(
    string Name,
    string? Description = null,
    string? Author = null,
    string License = "MIT",
    string Version = "0.1.0");
