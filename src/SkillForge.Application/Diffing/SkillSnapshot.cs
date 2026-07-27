using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Diffing;

/// <summary>
/// Everything known about one version of a skill, gathered so it can be compared with another.
/// </summary>
/// <remarks>
/// Loading, inspecting and validating already exist; a diff needs all three of their answers about both sides and
/// nothing else. Bundling them keeps <see cref="SkillSurfaceDiffer"/> pure — no file system, no ordering
/// concerns — which is why it can be tested by construction rather than by fixture.
/// </remarks>
/// <param name="Path">Path this version was read from.</param>
/// <param name="Skill">The loaded model.</param>
/// <param name="Inspection">What the skill contains and implies.</param>
/// <param name="Report">What validation found.</param>
public sealed record SkillSnapshot(
    string Path,
    SkillDefinition Skill,
    SkillInspection Inspection,
    ValidationReport Report);
