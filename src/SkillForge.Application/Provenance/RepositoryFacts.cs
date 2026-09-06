namespace SkillForge.Application.Provenance;

/// <summary>
/// What a git checkout says about itself.
/// </summary>
/// <param name="Root">Absolute path of the repository root, or <see langword="null"/> outside a repository.</param>
/// <param name="Repository">Remote URL the checkout points at, or <see langword="null"/> when there is none.</param>
/// <param name="Commit">Full commit SHA of the checkout, or <see langword="null"/> outside a repository.</param>
public sealed record RepositoryFacts(string? Root, string? Repository, string? Commit)
{
    /// <summary>Gets the answer for a directory that is not in a repository, or where git could not be run.</summary>
    public static RepositoryFacts None { get; } = new(null, null, null);

    /// <summary>Gets a value indicating whether the directory is inside a repository at all.</summary>
    public bool IsRepository => Root is { Length: > 0 };
}

/// <summary>
/// What <c>git status</c> said about one path.
/// </summary>
/// <remarks>
/// <paramref name="Unknown"/> exists so that "git could not answer" never reads as "nothing has changed". A status
/// that could not be obtained is not evidence of cleanliness, and the honest direction to fail in is the one that
/// stops a policy passing on a question nobody answered.
/// </remarks>
/// <param name="ModifiedPaths">Paths git reported as changed, as it printed them.</param>
/// <param name="Unknown">Whether the status could not be obtained.</param>
public sealed record WorkingTreeStatus(IReadOnlyList<string> ModifiedPaths, bool Unknown)
{
    /// <summary>Gets the answer for a path git was never able to report on.</summary>
    public static WorkingTreeStatus Unobtainable { get; } = new([], true);

    /// <summary>Gets the answer for a path with nothing uncommitted.</summary>
    public static WorkingTreeStatus Clean { get; } = new([], false);

    /// <summary>Gets a value indicating whether the path has, or may have, uncommitted changes.</summary>
    public bool IsDirty => Unknown || ModifiedPaths.Count > 0;
}
