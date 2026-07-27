namespace SkillForge.Application.Validation;

/// <summary>
/// A distinct local reference found in a skill's body, with its scope worked out.
/// </summary>
/// <param name="Target">The reference as written, using <c>/</c> separators.</param>
/// <param name="Line">One-based line of its first appearance in <c>SKILL.md</c>.</param>
/// <param name="Scope">How far out of the skill it reaches.</param>
/// <param name="PathInsideSkill">Collapsed skill-relative path, when the reference stays inside the skill.</param>
/// <param name="SiblingName">Name of the sibling directory, when the reference points at one.</param>
public sealed record SkillReference(
    string Target,
    int Line,
    ReferenceScope Scope,
    string? PathInsideSkill,
    string? SiblingName);
