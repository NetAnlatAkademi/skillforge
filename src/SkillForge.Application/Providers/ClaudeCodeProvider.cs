using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// Anthropic's Claude Code, the provider whose <c>SKILL.md</c> conventions the format came from.
/// </summary>
/// <remarks>
/// The two limits are the ones Anthropic documents for a skill's frontmatter. They are the only provider
/// limits SkillForge currently checks anything against; every other profile recognises its identifier and
/// declares no limits, which is stated in <c>docs/validation-rules.md</c> rather than left to be discovered.
/// </remarks>
public static class ClaudeCodeProvider
{
    /// <summary>The identifier written under <c>compatibility</c>.</summary>
    public const string Id = "claude-code";

    /// <summary>Gets the profile.</summary>
    public static AgentProviderProfile Profile { get; } = new(
        Id,
        "Claude Code",
        NameMaxLength: 64,
        DescriptionMaxLength: 1024,
        DocumentationUrl: "https://docs.claude.com/en/docs/agents-and-tools/agent-skills/overview");
}
