namespace SkillForge.Application.Provenance;

/// <summary>
/// Answers the two questions a repository can be asked about an asset: where it came from, and whether what is on
/// disk is what the commit says.
/// </summary>
/// <remarks>
/// Split from <see cref="IProvenanceReader"/> — which answers for exactly one skill, at packaging time — because a
/// scan over a tree asks the repository-wide questions once and the per-asset one many times. Asking all four git
/// questions per asset would multiply process launches by the number of skills for two answers that cannot differ.
/// </remarks>
public interface IRepositoryFactsReader
{
    /// <summary>Reads what the checkout containing a directory says about itself.</summary>
    /// <param name="directory">Directory to ask from.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The facts, with unobservable fields unset. Never fails.</returns>
    ValueTask<RepositoryFacts> ReadFactsAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>Reads which of a path's files have uncommitted changes.</summary>
    /// <param name="directory">Directory to ask from.</param>
    /// <param name="path">Path the question is scoped to.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The status, or <see cref="WorkingTreeStatus.Unobtainable"/> when git did not answer.</returns>
    ValueTask<WorkingTreeStatus> ReadStatusAsync(
        string directory,
        string path,
        CancellationToken cancellationToken = default);
}
