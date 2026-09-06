using SkillForge.Application.Provenance;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// <see cref="IRepositoryFactsReader"/> stub that answers whatever the test set up.
/// </summary>
/// <remarks>
/// Defaults to "not a repository", which is the case a provenance test most often needs: an asset nothing can be
/// traced from is the ordinary outcome, not the exceptional one.
/// </remarks>
internal sealed class FakeRepositoryFactsReader : IRepositoryFactsReader
{
    private readonly Dictionary<string, WorkingTreeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);

    internal RepositoryFacts Facts { get; set; } = RepositoryFacts.None;

    /// <summary>Registers uncommitted files under <paramref name="path"/>.</summary>
    internal FakeRepositoryFactsReader WithModified(string path, params string[] modifiedPaths)
    {
        _statuses[Normalise(path)] = new WorkingTreeStatus(modifiedPaths, false);
        return this;
    }

    /// <summary>Puts every asset inside a clean checkout of <paramref name="repository"/>.</summary>
    internal FakeRepositoryFactsReader InRepository(string root, string repository, string commit)
    {
        Facts = new RepositoryFacts(root, repository, commit);
        return this;
    }

    public ValueTask<RepositoryFacts> ReadFactsAsync(string directory, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Facts);

    public ValueTask<WorkingTreeStatus> ReadStatusAsync(
        string directory,
        string path,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_statuses.TryGetValue(Normalise(path), out var status)
            ? status
            : WorkingTreeStatus.Clean);

    private static string Normalise(string path) => path.Replace('\\', '/').TrimEnd('/');
}
