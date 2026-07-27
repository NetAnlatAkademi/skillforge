namespace SkillForge.Application.Validation;

/// <summary>
/// How far out of its own directory a reference in a skill's body reaches.
/// </summary>
/// <remarks>
/// The distinction exists because "cannot be packaged on its own" and "the author made a mistake" are different
/// claims, and only the second deserves an error. Measured on 229 real skills, treating every escaping reference
/// as an error produced 21 findings that were all legitimate cross-references inside one collection of skills.
/// </remarks>
public enum ReferenceScope
{
    /// <summary>The reference stays inside the skill directory.</summary>
    InsideSkill = 0,

    /// <summary>
    /// The reference goes up exactly one level and back down into a named directory — by construction, a
    /// sibling of this skill. A collection whose skills reference each other is a normal, reasonable pattern.
    /// </summary>
    SiblingSkill = 1,

    /// <summary>
    /// The reference reaches further than a sibling: two or more levels up, an absolute path, or the parent
    /// directory itself. Nothing about the skill's own layout can satisfy it.
    /// </summary>
    OutsideCollection = 2,
}
