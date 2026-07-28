using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// Cursor.
/// </summary>
/// <remarks>
/// Recognised, with no limits declared — see <see cref="CodexProvider"/> for why an unread limit is left
/// <see langword="null"/> rather than guessed at.
/// </remarks>
public static class CursorProvider
{
    /// <summary>The identifier written under <c>compatibility</c>.</summary>
    public const string Id = "cursor";

    /// <summary>Gets the profile.</summary>
    public static AgentProviderProfile Profile { get; } = new(
        Id,
        "Cursor",
        NameMaxLength: null,
        DescriptionMaxLength: null,
        DocumentationUrl: "https://docs.cursor.com/");
}
