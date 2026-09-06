namespace SkillForge.Domain.Provenance;

/// <summary>
/// What kind of thing provenance was recorded for.
/// </summary>
public enum AssetKind
{
    /// <summary>A directory with a <c>SKILL.md</c> in it.</summary>
    Skill,

    /// <summary>A directory with a plugin manifest in it.</summary>
    Plugin,
}
