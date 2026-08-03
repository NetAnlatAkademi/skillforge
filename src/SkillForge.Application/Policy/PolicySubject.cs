using SkillForge.Domain.Inspection;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Policy;

/// <summary>
/// Everything a policy is allowed to judge one skill on.
/// </summary>
/// <remarks>
/// Assembled by the caller from work that has already happened — the loader, the inspector, the skill's own
/// configuration and the provenance reader — so the evaluator itself touches no file system and asks nothing of
/// the network. A policy rule can only be written against something in here, which is what stops policies from
/// growing rules SkillForge cannot actually check.
/// </remarks>
/// <param name="Skill">The loaded skill.</param>
/// <param name="Inspection">What the skill's contents imply it can do.</param>
/// <param name="Configuration">What the skill declares about itself in <c>skillforge.yaml</c>.</param>
/// <param name="Provenance">Where the skill came from, as far as it could be observed.</param>
public sealed record PolicySubject(
    SkillDefinition Skill,
    SkillInspection Inspection,
    SkillConfiguration Configuration,
    SkillProvenance Provenance);
