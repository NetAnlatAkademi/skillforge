namespace SkillForge.Domain.Migration;

/// <summary>
/// One skill found in a provider's skill directory.
/// </summary>
/// <remarks>
/// An inventory entry, not a verdict: <c>migrate inspect</c> says what is installed where, and
/// <c>validate</c> is what judges it. The declared compatibility is carried because the interesting
/// observation in a migration is a skill installed for one provider while naming another.
/// </remarks>
/// <param name="ProviderId">Identifier of the provider whose directory it was found in.</param>
/// <param name="Name">The skill's declared name, or its directory name when the frontmatter has none.</param>
/// <param name="Directory">Absolute path of the skill's own directory.</param>
/// <param name="DeclaredCompatibility">Providers the skill lists under <c>compatibility</c>, as written.</param>
public sealed record SkillInventoryEntry(
    string ProviderId,
    string Name,
    string Directory,
    IReadOnlyList<string> DeclaredCompatibility);
