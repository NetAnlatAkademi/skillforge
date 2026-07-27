namespace SkillForge.Application.Validation;

/// <summary>
/// What <see cref="SkillRelativePath.Classify"/> worked out about a reference.
/// </summary>
/// <param name="Scope">How far out of the skill the reference reaches.</param>
/// <param name="PathInsideSkill">
/// The collapsed skill-relative path, set only when <see cref="Scope"/> is
/// <see cref="ReferenceScope.InsideSkill"/>.
/// </param>
/// <param name="SiblingName">
/// Name of the sibling directory, set only when <see cref="Scope"/> is
/// <see cref="ReferenceScope.SiblingSkill"/>.
/// </param>
public sealed record ReferenceClassification(
    ReferenceScope Scope,
    string? PathInsideSkill,
    string? SiblingName);
