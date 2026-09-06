using SkillForge.Application.Provenance;
using SkillForge.Application.Skills;
using SkillForge.Cli.Commands;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Provenance;
using SkillForge.Infrastructure.Yaml;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>provenance</c> and <c>provenance diff</c> over real directories on disk.
/// </summary>
/// <remarks>
/// Written against the real file system, the real manifest reader and the real hash: the parts worth testing here
/// are the ones a fake would have to imitate — that a plugin manifest is found where the convention puts it, that a
/// fingerprint changes when a file does, and that the diff notices. Only git is faked, so the tests do not need a
/// repository or the binary.
/// </remarks>
public sealed class ProvenanceCommandRunnerTests : IDisposable
{
    private const string Remote = "https://github.com/example/skills.git";
    private const string Commit = "43ab2c1f9e0d4b8a7c6e5f4a3b2c1d0e9f8a7b6c";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-provenance-tests",
        Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task ReportsASkillAndItsFingerprint()
    {
        var tree = Tree("before");
        WriteSkill(tree, "dotnet-api-review", version: "1.0.0");

        var output = Path.Combine(_root, "report.txt");
        var exitCode = await Runner().RunAsync(Request(tree, output: output), CancellationToken.None);

        exitCode.Should().Be(0);

        var report = await File.ReadAllTextAsync(output);
        report.Should().Contain("dotnet-api-review");
        report.Should().Contain("VERIFIED SOURCE");
        report.Should().Contain(Remote);
        report.Should().Contain("Publisher:    unknown");
    }

    [Fact]
    public async Task ReadsAPluginAndTheMarketplaceThatListsIt()
    {
        var tree = Tree("before");
        WritePlugin(tree, "company-deploy", publisher: "company", version: "1.4.2");
        WriteMarketplace(tree, "company-internal", "company", "company-deploy", revision: "v1.4.2");

        var output = Path.Combine(_root, "report.json");
        var exitCode = await Runner().RunAsync(
            Request(tree, format: OutputFormat.Json, output: output),
            CancellationToken.None);

        exitCode.Should().Be(0);

        var report = await File.ReadAllTextAsync(output);
        report.Should().Contain("\"publisher\": \"company\"");
        report.Should().Contain("\"marketplace\": \"company-internal\"");
        report.Should().Contain("\"updateMode\": \"pinned\"");
        report.Should().Contain("\"marketplaces\": 1");
    }

    [Fact]
    public async Task APathThatIsNotThereIsAUsageError()
    {
        var exitCode = await Runner().RunAsync(
            Request(Path.Combine(_root, "nowhere")),
            CancellationToken.None);

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task IdenticalTreesReportNoDrift()
    {
        var before = Tree("before");
        var after = Tree("after");
        WriteSkill(before, "demo", version: "1.0.0");
        WriteSkill(after, "demo", version: "1.0.0");

        var output = Path.Combine(_root, "diff.txt");
        var exitCode = await Runner().DiffAsync(
            DiffRequest(before, after, output: output),
            CancellationToken.None);

        exitCode.Should().Be(0);
        (await File.ReadAllTextAsync(output)).Should().Contain("say the same thing");
    }

    [Fact]
    public async Task AChangedPublisherIsReportedAsSF5401()
    {
        var (before, after) = Plugins(beforePublisher: "company", afterPublisher: "unknown-publisher");

        var output = Path.Combine(_root, "diff.txt");
        var exitCode = await Runner().DiffAsync(
            DiffRequest(before, after, output: output),
            CancellationToken.None);

        exitCode.Should().Be(0);

        var report = await File.ReadAllTextAsync(output);
        report.Should().Contain("SF5401");
        report.Should().Contain("company");
    }

    [Fact]
    public async Task DriftFailsTheRunOnlyWhenAsked()
    {
        var (before, after) = Plugins(beforePublisher: "company", afterPublisher: "someone-else");

        var exitCode = await Runner().DiffAsync(
            DiffRequest(before, after, failOnDrift: true),
            CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task APinThatBecameABranchIsReportedAsSF5302()
    {
        var before = Tree("before");
        var after = Tree("after");

        WritePlugin(before, "company-deploy", "company", "1.4.2");
        WriteMarketplace(before, "company-internal", "company", "company-deploy", revision: "v1.4.2");

        WritePlugin(after, "company-deploy", "company", "1.4.2");
        WriteMarketplace(after, "company-internal", "company", "company-deploy", revision: "main");

        var output = Path.Combine(_root, "diff.txt");
        await Runner().DiffAsync(DiffRequest(before, after, output: output), CancellationToken.None);

        (await File.ReadAllTextAsync(output)).Should().Contain("SF5302");
    }

    [Fact]
    public async Task ContentThatChangedUnderTheSameVersionIsReportedAsSF5501()
    {
        var before = Tree("before");
        var after = Tree("after");
        WriteSkill(before, "demo", version: "1.0.0");
        WriteSkill(after, "demo", version: "1.0.0", body: "# demo, quietly rewritten");

        var output = Path.Combine(_root, "diff.txt");
        await Runner().DiffAsync(DiffRequest(before, after, output: output), CancellationToken.None);

        (await File.ReadAllTextAsync(output)).Should().Contain("SF5501");
    }

    [Fact]
    public async Task ASkillThatArrivedWithNoTraceableOriginIsReportedAsSF5101()
    {
        var before = Tree("before");
        var after = Tree("after");
        WriteSkill(after, "arrived-from-nowhere");

        var output = Path.Combine(_root, "diff.txt");
        await DiffAsync(
            DiffRequest(before, after, output: output),
            outsideARepository: true,
            CancellationToken.None);

        (await File.ReadAllTextAsync(output)).Should().Contain("SF5101");
    }

    [Fact]
    public async Task TheSarifOutputIsTheSharedOne()
    {
        var (before, after) = Plugins("company", "someone-else");

        var output = Path.Combine(_root, "diff.sarif");
        await Runner().DiffAsync(
            DiffRequest(before, after, format: OutputFormat.Sarif, output: output),
            CancellationToken.None);

        var sarif = await File.ReadAllTextAsync(output);
        sarif.Should().Contain("\"version\": \"2.1.0\"");
        sarif.Should().Contain("SF5401");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private (string Before, string After) Plugins(string beforePublisher, string afterPublisher)
    {
        var before = Tree("before");
        var after = Tree("after");

        WritePlugin(before, "company-deploy", beforePublisher, "1.4.2");
        WritePlugin(after, "company-deploy", afterPublisher, "1.4.2");

        return (before, after);
    }

    private ProvenanceCommandRunner Runner(bool outsideARepository = false)
    {
        var fileSystem = new FileSystem();
        var loader = new SkillLoader(
            fileSystem,
            new YamlFrontmatterParser(),
            new YamlSkillConfigurationReader(fileSystem));

        return new ProvenanceCommandRunner(
            new ProvenanceInspector(
                fileSystem,
                new SkillDiscovery(fileSystem),
                loader,
                new StubRepositoryFactsReader(outsideARepository ? null : _root, Remote, Commit),
                new JsonDistributionManifestReader(fileSystem),
                new AssetFingerprinter(fileSystem, new Sha256HashCalculator())),
            fileSystem,
            [new JsonReportSerializer(), new SarifReportSerializer()]);
    }

    private static ProvenanceRequest Request(
        string path,
        string format = OutputFormat.Console,
        string? output = null) =>
        new(path, format, output, new Application.Abstractions.ReportRenderOptions());

    private static ProvenanceDiffRequest DiffRequest(
        string before,
        string after,
        bool failOnDrift = false,
        string format = OutputFormat.Console,
        string? output = null) =>
        new(before, after, failOnDrift, format, output, new Application.Abstractions.ReportRenderOptions());

    private string Tree(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteSkill(string tree, string name, string? version = null, string body = "# demo")
    {
        var directory = Path.Combine(tree, "skills", name);
        Directory.CreateDirectory(directory);

        var metadata = version is null ? string.Empty : $"metadata:{Environment.NewLine}  version: {version}{Environment.NewLine}";

        File.WriteAllText(
            Path.Combine(directory, "SKILL.md"),
            $"""
            ---
            name: {name}
            description: Use this skill when tracing where a skill came from in a test.
            license: MIT
            {metadata}---

            {body}
            """);
    }

    private static void WritePlugin(string tree, string name, string publisher, string version)
    {
        var directory = Path.Combine(tree, "plugins", name, ".claude-plugin");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "plugin.json"),
            $$"""
            { "name": "{{name}}", "version": "{{version}}", "author": "{{publisher}}" }
            """);
    }

    private static void WriteMarketplace(
        string tree,
        string name,
        string publisher,
        string plugin,
        string revision)
    {
        var directory = Path.Combine(tree, ".claude-plugin");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "marketplace.json"),
            $$"""
            {
              "name": "{{name}}",
              "owner": "{{publisher}}",
              "plugins": [
                { "name": "{{plugin}}", "source": { "repo": "example/plugins", "ref": "{{revision}}" } }
              ]
            }
            """);
    }

    private async Task<int> DiffAsync(
        ProvenanceDiffRequest request,
        bool outsideARepository,
        CancellationToken cancellationToken) =>
        await Runner(outsideARepository).DiffAsync(request, cancellationToken);

    /// <summary>Answers the two git questions without needing git, or a repository, to be there.</summary>
    private sealed class StubRepositoryFactsReader : IRepositoryFactsReader
    {
        private readonly RepositoryFacts _facts;

        internal StubRepositoryFactsReader(string? root, string repository, string commit) =>
            _facts = root is null ? RepositoryFacts.None : new RepositoryFacts(root, repository, commit);

        public ValueTask<RepositoryFacts> ReadFactsAsync(
            string directory,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_facts);

        public ValueTask<WorkingTreeStatus> ReadStatusAsync(
            string directory,
            string path,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(WorkingTreeStatus.Clean);
    }
}
