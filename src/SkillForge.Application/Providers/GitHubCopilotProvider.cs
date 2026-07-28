using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// GitHub Copilot.
/// </summary>
/// <remarks>
/// Recognised, with no limits declared — see <see cref="CodexProvider"/> for why an unread limit is left
/// <see langword="null"/> rather than guessed at.
/// </remarks>
public static class GitHubCopilotProvider
{
    /// <summary>The identifier written under <c>compatibility</c>.</summary>
    public const string Id = "github-copilot";

    /// <summary>Gets the profile.</summary>
    public static AgentProviderProfile Profile { get; } = new(
        Id,
        "GitHub Copilot",
        NameMaxLength: null,
        DescriptionMaxLength: null,
        DocumentationUrl: "https://docs.github.com/en/copilot");
}
