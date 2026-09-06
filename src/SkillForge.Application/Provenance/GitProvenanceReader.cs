using SkillForge.Application.Abstractions;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Reads provenance by asking git four read-only questions.
/// </summary>
/// <remarks>
/// <c>rev-parse --show-toplevel</c>, <c>rev-parse HEAD</c>, <c>remote get-url origin</c>, and a
/// <c>status --porcelain</c> scoped to the skill. Nothing is written, no revision is checked out, and a repository
/// that answers none of them produces provenance with those fields unset rather than an error: packaging a skill
/// from a plain directory is legitimate, it simply cannot be traced back to anything.
///
/// It answers for one skill at packaging time (<see cref="IProvenanceReader"/>) and for a whole tree at scan time
/// (<see cref="IRepositoryFactsReader"/>) from the same four questions, so there is one place in the product that
/// knows how to ask git anything.
///
/// The status question is scoped to the asset's own path on purpose. A repository with unrelated work in progress
/// would otherwise make every skill packaged from it look modified, and a dirty flag that is always set is a flag
/// nobody reads.
/// </remarks>
public sealed class GitProvenanceReader : IProvenanceReader, IRepositoryFactsReader
{
    private const string Git = "git";

    private readonly IProcessRunner _processRunner;
    private readonly TimeProvider _timeProvider;
    private readonly string _toolVersion;

    /// <summary>Initialises the reader.</summary>
    /// <param name="processRunner">Runs git.</param>
    /// <param name="timeProvider">Supplies the timestamp. Injected so tests can pin it.</param>
    /// <param name="toolVersion">
    /// Version to record. Passed in rather than read here, so one place in the process decides what version
    /// SkillForge claims to be.
    /// </param>
    public GitProvenanceReader(IProcessRunner processRunner, TimeProvider timeProvider, string toolVersion)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolVersion);

        _processRunner = processRunner;
        _timeProvider = timeProvider;
        _toolVersion = toolVersion;
    }

    /// <inheritdoc />
    public async ValueTask<SkillProvenance> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);

        var facts = await ReadFactsAsync(skillDirectory, cancellationToken).ConfigureAwait(false);

        if (facts.Root is not { Length: > 0 } root)
        {
            // Not a repository, or git is not installed. Neither lets SkillForge name a source.
            return new SkillProvenance(null, null, null, false, _toolVersion, _timeProvider.GetUtcNow());
        }

        var status = await ReadStatusAsync(skillDirectory, skillDirectory, cancellationToken).ConfigureAwait(false);

        return new SkillProvenance(
            facts.Repository,
            facts.Commit,
            RelativePath(root, skillDirectory),
            status.IsDirty,
            _toolVersion,
            _timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public async ValueTask<RepositoryFacts> ReadFactsAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = await AskAsync(directory, ["rev-parse", "--show-toplevel"], cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return RepositoryFacts.None;
        }

        var commit = await AskAsync(directory, ["rev-parse", "HEAD"], cancellationToken)
            .ConfigureAwait(false);

        var repository = await AskAsync(directory, ["remote", "get-url", "origin"], cancellationToken)
            .ConfigureAwait(false);

        return new RepositoryFacts(root, WithoutCredentials(repository), commit);
    }

    /// <summary>
    /// Removes anything before the <c>@</c> in a remote URL's authority.
    /// </summary>
    /// <remarks>
    /// **This is the one place a secret can enter SkillForge's output, so it is the one place that removes it.**
    /// An authenticated HTTPS remote is ordinary — GitHub Actions' own checkout writes
    /// <c>https://x-access-token:&lt;token&gt;@github.com/...</c>, and a developer using a personal access token over
    /// HTTPS has <c>https://&lt;token&gt;@github.com/...</c>. <c>git remote get-url</c> returns the token intact, and
    /// this value is printed by <c>provenance</c>, written into a package manifest by <c>pack</c>, and compared by
    /// <c>provenance diff</c>.
    ///
    /// The whole user-info component goes, not just the part after a colon: a bare
    /// <c>https://ghp_xxx@github.com/...</c> carries the token in the user field with no password beside it, so
    /// keeping "the username" would keep exactly the thing being removed. What is lost is the <c>git</c> in
    /// <c>ssh://git@host/repo</c>, which identifies nothing.
    ///
    /// An scp-style remote (<c>git@github.com:org/repo.git</c>) is not an absolute URI and is returned unchanged.
    /// That form carries no password by construction — it names an SSH user, and the key lives elsewhere.
    /// </remarks>
    private static string? WithoutCredentials(string? remoteUrl)
    {
        if (remoteUrl is not { Length: > 0 }
            || !Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length == 0)
        {
            return remoteUrl;
        }

        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.ToString();
    }

    /// <inheritdoc />
    public async ValueTask<WorkingTreeStatus> ReadStatusAsync(
        string directory,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var status = await _processRunner.RunAsync(
            Git,
            ["status", "--porcelain", "--", path],
            directory,
            cancellationToken).ConfigureAwait(false);

        // A status that could not be obtained is not evidence of cleanliness, so it reads as dirty: the honest
        // direction to fail in is the one that stops a policy from passing on a question nobody answered.
        if (status is not { Succeeded: true })
        {
            return WorkingTreeStatus.Unobtainable;
        }

        return status.StandardOutput.Length == 0
            ? WorkingTreeStatus.Clean
            : new WorkingTreeStatus(
                [.. status.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                false);
    }

    /// <summary>Runs a git command and returns its output, or <see langword="null"/> when it did not succeed.</summary>
    private async ValueTask<string?> AskAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await _processRunner
            .RunAsync(Git, arguments, workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        return result is { Succeeded: true, StandardOutput.Length: > 0 } ? result.StandardOutput : null;
    }

    /// <summary>
    /// Expresses the skill's directory relative to the repository root, with forward slashes. An absolute path
    /// from a build agent means nothing to whoever reads the manifest later.
    /// </summary>
    private static string RelativePath(string repositoryRoot, string skillDirectory)
    {
        var relative = Path.GetRelativePath(repositoryRoot, skillDirectory).Replace('\\', '/');

        return relative.Length == 0 ? "." : relative;
    }
}
