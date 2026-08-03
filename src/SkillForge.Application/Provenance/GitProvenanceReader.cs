using SkillForge.Application.Abstractions;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Reads provenance by asking git three read-only questions.
/// </summary>
/// <remarks>
/// <c>rev-parse --show-toplevel</c>, <c>rev-parse HEAD</c>, <c>remote get-url origin</c>, and a
/// <c>status --porcelain</c> scoped to the skill. Nothing is written, no revision is checked out, and a repository
/// that answers none of them produces provenance with those fields unset rather than an error: packaging a skill
/// from a plain directory is legitimate, it simply cannot be traced back to anything.
///
/// The status question is scoped to the skill's own path on purpose. A repository with unrelated work in progress
/// would otherwise make every skill packaged from it look modified, and a dirty flag that is always set is a flag
/// nobody reads.
/// </remarks>
public sealed class GitProvenanceReader : IProvenanceReader
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

        var root = await AskAsync(skillDirectory, ["rev-parse", "--show-toplevel"], cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            // Not a repository, or git is not installed. Neither lets SkillForge name a source.
            return new SkillProvenance(null, null, null, false, _toolVersion, _timeProvider.GetUtcNow());
        }

        var commit = await AskAsync(skillDirectory, ["rev-parse", "HEAD"], cancellationToken)
            .ConfigureAwait(false);

        var repository = await AskAsync(skillDirectory, ["remote", "get-url", "origin"], cancellationToken)
            .ConfigureAwait(false);

        var status = await _processRunner.RunAsync(
            Git,
            ["status", "--porcelain", "--", skillDirectory],
            skillDirectory,
            cancellationToken).ConfigureAwait(false);

        // A status that could not be obtained is not evidence of cleanliness, so it reads as dirty: the honest
        // direction to fail in is the one that stops a policy from passing on a question nobody answered.
        var isDirty = status is not { Succeeded: true } || status.StandardOutput.Length > 0;

        return new SkillProvenance(
            repository,
            commit,
            RelativePath(root, skillDirectory),
            isDirty,
            _toolVersion,
            _timeProvider.GetUtcNow());
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
