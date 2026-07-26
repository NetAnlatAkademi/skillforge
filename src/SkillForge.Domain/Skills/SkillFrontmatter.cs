namespace SkillForge.Domain.Skills;

/// <summary>
/// The YAML frontmatter block at the top of a <c>SKILL.md</c> file.
/// </summary>
/// <remarks>
/// Fields are nullable and collections may be empty: the loader's job is to report what the file
/// actually says, not to insist that it is complete. Whether a missing field is a problem is decided by
/// the validation rules.
/// </remarks>
/// <param name="Name">Value of the <c>name</c> field.</param>
/// <param name="Description">Value of the <c>description</c> field.</param>
/// <param name="License">Value of the <c>license</c> field.</param>
/// <param name="Compatibility">Agents listed under <c>compatibility</c>.</param>
/// <param name="AllowedTools">Tools listed under <c>allowed-tools</c>.</param>
/// <param name="Metadata">Scalar entries under <c>metadata</c>, for example <c>author</c> and <c>version</c>.</param>
/// <param name="StartLine">One-based line of the opening <c>---</c> delimiter in <c>SKILL.md</c>.</param>
/// <param name="EndLine">One-based line of the closing <c>---</c> delimiter in <c>SKILL.md</c>.</param>
public sealed record SkillFrontmatter(
    string? Name,
    string? Description,
    string? License,
    IReadOnlyList<string> Compatibility,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyDictionary<string, string> Metadata,
    int StartLine,
    int EndLine)
{
    /// <summary>
    /// Frontmatter with no fields set, used when a block is present but empty.
    /// </summary>
    /// <param name="startLine">One-based line of the opening delimiter.</param>
    /// <param name="endLine">One-based line of the closing delimiter.</param>
    /// <returns>An empty frontmatter instance.</returns>
    public static SkillFrontmatter Empty(int startLine, int endLine) =>
        new(null, null, null, [], [], new Dictionary<string, string>(StringComparer.Ordinal), startLine, endLine);

    /// <summary>
    /// Gets the declared package version from <c>metadata.version</c>, or <see langword="null"/> when absent.
    /// </summary>
    public string? Version => Metadata.TryGetValue("version", out var version) ? version : null;

    /// <summary>
    /// Gets the declared author from <c>metadata.author</c>, or <see langword="null"/> when absent.
    /// </summary>
    public string? Author => Metadata.TryGetValue("author", out var author) ? author : null;
}
