namespace SkillForge.Domain.Provenance;

/// <summary>
/// Where a packaged skill came from.
/// </summary>
/// <remarks>
/// Answers one question — "can I tell where this came from and check it" — and answers it with what was observed
/// rather than with what was hoped for. Every field about the source is nullable, because a skill packaged outside
/// a repository has no repository, no commit and no repository-relative path, and inventing values for those would
/// make provenance worse than useless: a consumer would believe it.
///
/// It is deliberately **not** a signature. Nothing here proves the skill was not modified by whoever wrote the
/// manifest; it records what the working copy said at the time. Cryptographic signing is a later concern, and
/// calling this "verified" would be the claim SkillForge does not make.
/// </remarks>
/// <param name="Repository">Remote URL the checkout points at, or <see langword="null"/> when there is none.</param>
/// <param name="Commit">Full commit SHA of the checkout, or <see langword="null"/> outside a repository.</param>
/// <param name="Path">
/// The skill's path relative to the repository root, using forward slashes, or <see langword="null"/> outside a
/// repository. Repository-relative rather than absolute: an agent's build directory means nothing to a consumer.
/// </param>
/// <param name="WorkingTreeIsDirty">
/// Whether the skill's own files had uncommitted changes when it was packaged. Recorded because a commit SHA next
/// to a modified working copy names a commit whose contents are not what was packaged, and a consumer reading only
/// the SHA would never know.
/// </param>
/// <param name="ToolVersion">Version of SkillForge that produced the package.</param>
/// <param name="GeneratedAtUtc">When the provenance was recorded, in UTC.</param>
public sealed record SkillProvenance(
    string? Repository,
    string? Commit,
    string? Path,
    bool WorkingTreeIsDirty,
    string ToolVersion,
    DateTimeOffset GeneratedAtUtc)
{
    /// <summary>
    /// Gets a value indicating whether the source can be identified at all — a repository, a commit and a path
    /// within it, with nothing uncommitted.
    /// </summary>
    /// <remarks>
    /// What a policy asking for provenance is really asking about. A dirty working tree fails it on purpose: the
    /// commit is named, but it is not what was packaged.
    /// </remarks>
    public bool IdentifiesItsSource =>
        Repository is { Length: > 0 }
        && Commit is { Length: > 0 }
        && Path is { Length: > 0 }
        && !WorkingTreeIsDirty;
}
