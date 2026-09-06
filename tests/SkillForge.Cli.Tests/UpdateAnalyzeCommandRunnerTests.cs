using SkillForge.Application.Inspection;
using SkillForge.Application.Migration;
using SkillForge.Application.Provenance;
using SkillForge.Application.Skills;
using SkillForge.Application.Updates;
using SkillForge.Cli.Commands;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Provenance;
using SkillForge.Infrastructure.Yaml;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>update analyze</c> over two real directories.
/// </summary>
/// <remarks>
/// End to end against the real readers, because the interesting failures are in the reading: a hook file found by
/// path, an MCP server read out of a <c>.mcp.json</c> that is nested three directories down, an environment
/// variable name that looks like a credential. A fake that returned those would be testing the fake.
/// </remarks>
public sealed class UpdateAnalyzeCommandRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-update-tests",
        Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task AnUpdateThatChangesNothingReachableSaysSo()
    {
        var (before, after) = Trees();
        WriteSkill(before, "demo");
        WriteSkill(after, "demo");

        var report = await Analyze(before, after);

        report.Should().Contain("Risk:   INFORMATIONAL");
        report.Should().Contain("reaches exactly as far");
    }

    [Fact]
    public async Task ANewMcpServerAndItsCredentialAreBothReported()
    {
        var (before, after) = Trees();
        WriteSkill(before, "demo");
        WriteSkill(after, "demo");
        WriteMcp(after, "deployment-prod", "https://deploy.company.com/mcp", "DEPLOY_TOKEN");

        var report = await Analyze(before, after);

        report.Should().Contain("+ MCP server: deployment-prod");
        report.Should().Contain("+ host: deploy.company.com");
        report.Should().Contain("+ credential source: DEPLOY_TOKEN");
    }

    [Fact]
    public async Task TheValueOfACredentialIsNeverRead()
    {
        var (before, after) = Trees();
        WriteSkill(before, "demo");
        WriteSkill(after, "demo");
        WriteMcp(after, "deployment-prod", "https://deploy.company.com/mcp", "DEPLOY_TOKEN");

        var report = await Analyze(before, after, format: OutputFormat.Json);

        report.Should().Contain("DEPLOY_TOKEN");
        report.Should().NotContain("sk-do-not-print-me");
    }

    [Fact]
    public async Task AnAutomaticUpdateThatAddsAScriptIsHighAndCoded()
    {
        var (before, after) = Trees();
        WriteSkill(before, "demo");
        WriteSkill(after, "demo");
        WriteScript(after, "demo", "install.sh");
        WritePlugin(after, "company-deploy", "company");
        WritePlugin(before, "company-deploy", "company");
        WriteMarketplace(after, "company-deploy", automaticUpdate: true);

        var report = await Analyze(before, after);

        report.Should().Contain("Risk:   HIGH");
        report.Should().Contain("SF5303");
        report.Should().Contain("install.sh");
    }

    [Fact]
    public async Task APublisherChangeUnderAnAutomaticUpdateIsCritical()
    {
        var (before, after) = Trees();
        WritePlugin(before, "company-deploy", "company");
        WritePlugin(after, "company-deploy", "someone-else");
        WriteMarketplace(after, "company-deploy", automaticUpdate: true);

        var report = await Analyze(before, after);

        report.Should().Contain("Risk:   CRITICAL");
        report.Should().Contain("SF5401");
    }

    [Fact]
    public async Task ExpansionFailsTheRunOnlyWhenAsked()
    {
        var (before, after) = Trees();
        WriteSkill(before, "demo");
        WriteSkill(after, "demo");
        WriteScript(after, "demo", "install.sh");

        (await Run(before, after)).Should().Be(0);
        (await Run(before, after, failOnExpansion: true)).Should().Be(1);
    }

    [Fact]
    public async Task APathThatIsNotThereIsAUsageError()
    {
        var (before, _) = Trees();

        (await Run(before, Path.Combine(_root, "nowhere"))).Should().Be(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<string> Analyze(string before, string after, string format = OutputFormat.Console)
    {
        var output = Path.Combine(_root, "analysis.txt");

        await Run(before, after, format: format, output: output);

        return await File.ReadAllTextAsync(output);
    }

    private static async Task<int> Run(
        string before,
        string after,
        bool failOnExpansion = false,
        string format = OutputFormat.Console,
        string? output = null)
    {
        var fileSystem = new FileSystem();
        var loader = new SkillLoader(
            fileSystem,
            new YamlFrontmatterParser(),
            new YamlSkillConfigurationReader(fileSystem));
        var discovery = new SkillDiscovery(fileSystem);

        var runner = new UpdateAnalyzeCommandRunner(
            new ProvenanceInspector(
                fileSystem,
                discovery,
                loader,
                new StubFacts(),
                new JsonDistributionManifestReader(fileSystem),
                new AssetFingerprinter(fileSystem, new Sha256HashCalculator())),
            new CapabilitySurfaceScanner(
                fileSystem,
                discovery,
                loader,
                new SkillInspector(),
                [new JsonMcpConfigurationReader(fileSystem)]),
            fileSystem,
            [new JsonReportSerializer(), new SarifReportSerializer()]);

        return await runner.RunAsync(
            new UpdateAnalyzeRequest(
                before,
                after,
                failOnExpansion,
                format,
                output,
                new Application.Abstractions.ReportRenderOptions()),
            CancellationToken.None);
    }

    private (string Before, string After) Trees()
    {
        var before = Path.Combine(_root, "v1");
        var after = Path.Combine(_root, "v2");
        Directory.CreateDirectory(before);
        Directory.CreateDirectory(after);

        return (before, after);
    }

    private static void WriteSkill(string tree, string name)
    {
        var directory = Path.Combine(tree, "skills", name);
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "SKILL.md"),
            $"""
            ---
            name: {name}
            description: Use this skill when analysing what an update does in a test.
            license: MIT
            ---

            # {name}
            """);
    }

    private static void WriteScript(string tree, string skill, string fileName)
    {
        var directory = Path.Combine(tree, "skills", skill, "scripts");
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, fileName), "echo hello");
    }

    private static void WriteMcp(string tree, string server, string url, string environmentVariable)
    {
        File.WriteAllText(
            Path.Combine(tree, ".mcp.json"),
            $$"""
            {
              "mcpServers": {
                "{{server}}": {
                  "type": "http",
                  "url": "{{url}}",
                  "env": { "{{environmentVariable}}": "sk-do-not-print-me" }
                }
              }
            }
            """);
    }

    private static void WritePlugin(string tree, string name, string publisher)
    {
        var directory = Path.Combine(tree, "plugins", name, ".claude-plugin");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "plugin.json"),
            $$"""
            { "name": "{{name}}", "version": "1.4.2", "author": "{{publisher}}" }
            """);
    }

    private static void WriteMarketplace(string tree, string plugin, bool automaticUpdate)
    {
        var directory = Path.Combine(tree, ".claude-plugin");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "marketplace.json"),
            $$"""
            {
              "name": "company-internal",
              "owner": "company",
              "plugins": [
                { "name": "{{plugin}}", "autoUpdate": {{(automaticUpdate ? "true" : "false")}} }
              ]
            }
            """);
    }

    /// <summary>Outside a repository, so the tests need neither git nor a checkout.</summary>
    private sealed class StubFacts : IRepositoryFactsReader
    {
        public ValueTask<RepositoryFacts> ReadFactsAsync(
            string directory,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(RepositoryFacts.None);

        public ValueTask<WorkingTreeStatus> ReadStatusAsync(
            string directory,
            string path,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(WorkingTreeStatus.Clean);
    }
}
