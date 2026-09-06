using SkillForge.Application.Abstractions;
using SkillForge.Application.Provenance;

namespace SkillForge.Application.Tests.Provenance;

/// <summary>
/// Provenance records what was observed. Every test here is about a case where something could not be observed,
/// because inventing a value there is the failure mode that makes provenance worse than having none.
/// </summary>
public sealed class GitProvenanceReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InsideACleanRepositoryEveryFieldIsAnswered()
    {
        var reader = Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = "https://github.com/example/skills.git",
            Status = string.Empty,
        });

        var provenance = await reader.ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.Repository.Should().Be("https://github.com/example/skills.git");
        provenance.Commit.Should().Be("abc123def4567890abc123def4567890abc123de");
        provenance.Path.Should().Be("skills/demo");
        provenance.WorkingTreeIsDirty.Should().BeFalse();
        provenance.IdentifiesItsSource.Should().BeTrue();
    }

    [Fact]
    public async Task TheToolVersionAndTimestampAreAlwaysRecorded()
    {
        var provenance = await Reader(new FakeProcessRunner()).ReadAsync("/tmp/demo", CancellationToken.None);

        provenance.ToolVersion.Should().Be("26.215.1");
        provenance.GeneratedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task OutsideARepositoryTheSourceFieldsAreNullRatherThanGuessed()
    {
        var provenance = await Reader(new FakeProcessRunner()).ReadAsync("/tmp/demo", CancellationToken.None);

        provenance.Repository.Should().BeNull();
        provenance.Commit.Should().BeNull();
        provenance.Path.Should().BeNull();
        provenance.IdentifiesItsSource.Should().BeFalse();
    }

    [Fact]
    public async Task WithNoGitInstalledTheAnswerIsTheSameAsOutsideARepository()
    {
        // "git could not be started" and "this is not a repository" are different facts, but neither one lets
        // SkillForge name a source, so neither one produces a value.
        var provenance = await Reader(new FakeProcessRunner { GitIsMissing = true })
            .ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.Commit.Should().BeNull();
        provenance.IdentifiesItsSource.Should().BeFalse();
    }

    [Fact]
    public async Task ARepositoryWithNoRemoteStillReportsItsCommit()
    {
        var provenance = await Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = null,
            Status = string.Empty,
        }).ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.Repository.Should().BeNull();
        provenance.Commit.Should().NotBeNull();

        // A commit nobody else can fetch does not identify a source.
        provenance.IdentifiesItsSource.Should().BeFalse();
    }

    [Fact]
    public async Task UncommittedChangesToTheSkillAreReported()
    {
        var provenance = await Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = "https://github.com/example/skills.git",
            Status = " M skills/demo/SKILL.md",
        }).ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.WorkingTreeIsDirty.Should().BeTrue();

        // The commit is named, but it is not what was packaged.
        provenance.IdentifiesItsSource.Should().BeFalse();
    }

    [Fact]
    public async Task StatusIsAskedAboutTheSkillRatherThanTheWholeRepository()
    {
        // A repository with unrelated work in progress must not make every skill packaged from it look modified.
        var runner = new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = "https://github.com/example/skills.git",
            Status = string.Empty,
        };

        await Reader(runner).ReadAsync("/repo/skills/demo", CancellationToken.None);

        runner.StatusArguments.Should().Contain("/repo/skills/demo");
    }

    [Fact]
    public async Task ASkillAtTheRepositoryRootHasARelativePathOfDot()
    {
        var provenance = await Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = "https://github.com/example/skills.git",
            Status = string.Empty,
        }).ReadAsync("/repo", CancellationToken.None);

        provenance.Path.Should().Be(".");
    }

    [Theory]
    [InlineData("https://x-access-token:ghs_secret@github.com/example/skills.git")]
    [InlineData("https://ghp_secret@github.com/example/skills.git")]
    [InlineData("https://someone:hunter2@github.com/example/skills.git")]
    public async Task ACredentialInTheRemoteUrlNeverReachesTheReport(string remoteUrl)
    {
        // GitHub Actions' own checkout writes an authenticated remote, and so does anyone using a personal access
        // token over HTTPS. 'git remote get-url' returns the token intact, and this value is printed by
        // 'provenance', written into a package manifest by 'pack' and compared by 'provenance diff'.
        var provenance = await Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = remoteUrl,
            Status = string.Empty,
        }).ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.Repository.Should().Be("https://github.com/example/skills.git");
        provenance.Repository.Should().NotContain("secret");
        provenance.Repository.Should().NotContain("hunter2");
    }

    [Fact]
    public async Task AnScpStyleRemoteIsLeftAlone()
    {
        // Not an absolute URI, and it carries no password by construction: it names an SSH user and the key lives
        // elsewhere. Rewriting it would lose the remote without removing anything.
        var provenance = await Reader(new FakeProcessRunner
        {
            TopLevel = "/repo",
            Commit = "abc123def4567890abc123def4567890abc123de",
            RemoteUrl = "git@github.com:example/skills.git",
            Status = string.Empty,
        }).ReadAsync("/repo/skills/demo", CancellationToken.None);

        provenance.Repository.Should().Be("git@github.com:example/skills.git");
    }

    private static GitProvenanceReader Reader(IProcessRunner runner) =>
        new(runner, new FakeTimeProvider(Now), "26.215.1");

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Answers the three questions git is asked, and records how the third one was asked.</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        internal bool GitIsMissing { get; init; }

        internal string? TopLevel { get; init; }

        internal string? Commit { get; init; }

        internal string? RemoteUrl { get; init; }

        internal string Status { get; init; } = string.Empty;

        internal IReadOnlyList<string> StatusArguments { get; private set; } = [];

        public ValueTask<ProcessResult?> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            if (GitIsMissing)
            {
                return ValueTask.FromResult<ProcessResult?>(null);
            }

            if (arguments.Contains("--show-toplevel"))
            {
                return Answer(TopLevel);
            }

            if (arguments.Contains("HEAD"))
            {
                return Answer(Commit);
            }

            if (arguments.Contains("get-url"))
            {
                return Answer(RemoteUrl);
            }

            StatusArguments = arguments;
            return Answer(TopLevel is null ? null : Status);
        }

        private static ValueTask<ProcessResult?> Answer(string? output) =>
            ValueTask.FromResult<ProcessResult?>(output is null
                ? new ProcessResult(128, string.Empty, "fatal: not a git repository")
                : new ProcessResult(0, output, string.Empty));
    }
}
