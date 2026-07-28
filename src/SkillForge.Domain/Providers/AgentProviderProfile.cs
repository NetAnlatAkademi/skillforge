namespace SkillForge.Domain.Providers;

/// <summary>
/// What SkillForge knows about one agent provider's skill format.
/// </summary>
/// <remarks>
/// Every limit is nullable, and a <see langword="null"/> limit means <em>not known</em> rather than
/// <em>no limit</em>: nothing is checked against it. A provider whose constraints have not been read from that
/// provider's own documentation still belongs here — recognising its identifier is what stops a legitimate
/// <c>compatibility</c> entry being reported as a typo.
/// </remarks>
/// <param name="Id">
/// The identifier written under <c>compatibility</c> in a skill's frontmatter, for example
/// <c>claude-code</c>. Compared case-insensitively.
/// </param>
/// <param name="DisplayName">The provider's name as a person writes it, used in messages.</param>
/// <param name="NameMaxLength">
/// Longest <c>name</c> the provider accepts, or <see langword="null"/> when SkillForge does not know it.
/// </param>
/// <param name="DescriptionMaxLength">
/// Longest <c>description</c> the provider accepts, or <see langword="null"/> when SkillForge does not know it.
/// </param>
/// <param name="DocumentationUrl">
/// Where the constraints above were read from, so a reader can check them rather than trust them.
/// </param>
public sealed record AgentProviderProfile(
    string Id,
    string DisplayName,
    int? NameMaxLength,
    int? DescriptionMaxLength,
    string? DocumentationUrl)
{
    /// <summary>
    /// Gets a value indicating whether this profile carries any limit SkillForge can check a skill against.
    /// </summary>
    /// <remarks>
    /// A profile that carries none is still useful — it makes the provider's identifier a known one — but it
    /// can never produce a finding, and the documentation says which providers those are.
    /// </remarks>
    public bool HasCheckableLimits => NameMaxLength is not null || DescriptionMaxLength is not null;
}
