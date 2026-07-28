using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// OpenAI's Codex CLI.
/// </summary>
/// <remarks>
/// No limits are declared. Codex is recognised so that declaring it is not reported as a typo, but SkillForge
/// has not read a documented frontmatter limit for it, and inventing one would produce findings about a
/// constraint that may not exist. A measured limit can be added here without touching the rules.
/// </remarks>
public static class CodexProvider
{
    /// <summary>The identifier written under <c>compatibility</c>.</summary>
    public const string Id = "codex";

    /// <summary>Gets the profile.</summary>
    public static AgentProviderProfile Profile { get; } = new(
        Id,
        "Codex",
        NameMaxLength: null,
        DescriptionMaxLength: null,
        DocumentationUrl: "https://developers.openai.com/codex/");
}
