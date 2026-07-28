using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// The default registry: an explicit list of the providers SkillForge ships knowledge of.
/// </summary>
/// <remarks>
/// Explicit for the same reason the rule list is (see <c>SkillValidationRules</c>): a provider that disappeared
/// because of how assemblies happened to load would turn every skill declaring it into a typo report.
/// </remarks>
public sealed class AgentProviderRegistry : IAgentProviderRegistry
{
    private readonly Dictionary<string, AgentProviderProfile> _byId;

    /// <summary>Initialises the registry with the providers SkillForge ships.</summary>
    public AgentProviderRegistry()
        : this(
            ClaudeCodeProvider.Profile,
            CodexProvider.Profile,
            GitHubCopilotProvider.Profile,
            CursorProvider.Profile)
    {
    }

    /// <summary>Initialises the registry with a given set of profiles, which is what tests want.</summary>
    /// <param name="profiles">The profiles to recognise.</param>
    public AgentProviderRegistry(params AgentProviderProfile[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        _byId = profiles.ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        Profiles = [.. profiles.OrderBy(profile => profile.Id, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentProviderProfile> Profiles { get; }

    /// <inheritdoc />
    public AgentProviderProfile? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _byId.GetValueOrDefault(id.Trim());
    }

    /// <inheritdoc />
    public string? Suggest(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return ProviderIdSuggestion.Closest(id, Profiles.Select(profile => profile.Id));
    }
}
