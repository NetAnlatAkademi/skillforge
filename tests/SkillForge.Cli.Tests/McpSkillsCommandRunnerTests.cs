using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using SkillForge.Application.Mcp;
using SkillForge.Cli.Commands;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Yaml;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>mcp skills inspect</c> and <c>mcp skills verify</c> as a user meets them: the command tree, the exit codes, what
/// each format carries, and what never reaches any of them.
/// </summary>
public sealed class McpSkillsCommandRunnerTests : IDisposable
{
    private const string UrlSecret = "sk-live-url-must-not-print";
    private const string GoodSkill = "skill://good/SKILL.md";
    private const string TamperedSkill = "skill://tampered/SKILL.md";
    private const string DynamicSkill = "skill://daily/SKILL.md";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-mcp-skills-cli-tests",
        Guid.NewGuid().ToString("n"));

    public McpSkillsCommandRunnerTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("mcp skills inspect ./mcp.json")]
    [InlineData("mcp skills inspect ./mcp.json --probe --format json -o out.json")]
    [InlineData("mcp skills verify ./mcp.json --probe")]
    [InlineData("mcp skills verify ./mcp.json --probe --skill skill://a/SKILL.md --skill skill://b/SKILL.md")]
    [InlineData("mcp skills verify ./mcp.json --probe --fail-on-mismatch --format sarif -o skills.sarif")]
    public void AcceptsTheDocumentedInvocations(string commandLine) =>
        Root().Parse(commandLine.Split(' ')).Errors.Should().BeEmpty();

    [Theory]
    [InlineData("mcp skills")]
    [InlineData("mcp skills ./mcp.json")]
    [InlineData("mcp skills install ./mcp.json")]
    [InlineData("mcp skills inspect ./mcp.json --format sarif")]
    [InlineData("mcp skills inspect ./mcp.json --fail-on-mismatch")]
    [InlineData("mcp skills inspect ./mcp.json --skill skill://a/SKILL.md")]
    public void RejectsWhatItDoesNotUnderstand(string commandLine) =>
        Root().Parse(commandLine.Split(' ')).Errors.Should().NotBeEmpty();

    [Fact]
    public void SkillsInspectAndMcpInspectAreDifferentCommandsWithTheSameName()
    {
        // System.CommandLine resolves a name within its parent, so 'inspect' under 'mcp skills' and 'inspect' under
        // 'mcp' cannot shadow each other. Pinned here because a future flattening of the tree would make one of them
        // silently disappear.
        var root = Root();
        var mcpGroup = root.Subcommands.Single(command => command.Name == "mcp");
        var skillsInspect = mcpGroup.Subcommands.Single(command => command.Name == "skills")
            .Subcommands.Single(command => command.Name == "inspect");
        var mcpInspect = mcpGroup.Subcommands.Single(command => command.Name == "inspect");

        root.Parse(["mcp", "skills", "inspect", "./mcp.json"]).CommandResult.Command.Should().BeSameAs(skillsInspect);
        root.Parse(["mcp", "inspect", "./mcp.json"]).CommandResult.Command.Should().BeSameAs(mcpInspect);
        skillsInspect.Should().NotBeSameAs(mcpInspect);

        // 'mcp inspect' keeps its own option, which 'mcp skills inspect' does not have.
        Root().Parse(["mcp", "inspect", "./mcp.json", "--probe-mcp"]).Errors.Should().BeEmpty();
        Root().Parse(["mcp", "skills", "inspect", "./mcp.json", "--probe-mcp"]).Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AMatchExitsZeroAndSaysWhatItDoesNotMean()
    {
        var output = Path.Combine(_root, "out.txt");

        var exit = await Run(verify: true, failOnMismatch: true, OutputFormat.Console, output, GoodSkill);

        exit.Should().Be(ExitCodes.Success);
        var text = await File.ReadAllTextAsync(output);
        text.Should().Contain("integrity match — not a statement about trust or safety");
        text.Should().Contain("does NOT mean trusted");
    }

    [Fact]
    public async Task AMismatchFailsOnlyWhenAsked()
    {
        var reported = await Run(verify: true, failOnMismatch: false, OutputFormat.Json, Path.Combine(_root, "a.json"), TamperedSkill);
        var gated = await Run(verify: true, failOnMismatch: true, OutputFormat.Json, Path.Combine(_root, "b.json"), TamperedSkill);

        reported.Should().Be(ExitCodes.Success, "verify reports evidence; a gate is opt-in");
        gated.Should().Be(ExitCodes.ValidationFailed);
    }

    [Fact]
    public async Task JsonCarriesTheEvidenceAndTheCaveat()
    {
        var output = Path.Combine(_root, "out.json");

        await Run(verify: true, failOnMismatch: false, OutputFormat.Json, output, GoodSkill, TamperedSkill);

        var json = await File.ReadAllTextAsync(output);
        json.Should().Contain("\"integrityMeans\":").And.Contain("not trust, not safety, not a verified publisher");
        json.Should().Contain("\"status\": \"IntegrityMatch\"");
        json.Should().Contain("\"status\": \"IntegrityMismatch\"");
        json.Should().Contain("\"serverIdentityVerified\": false");
        json.Should().Contain("\"origin\": \"McpServer\"");
        json.Should().Contain(DiagnosticCodes.McpSkillDigestMismatch);
    }

    [Fact]
    public async Task SarifCarriesIntegrityFindingsAndNothingInformational()
    {
        var output = Path.Combine(_root, "out.sarif");

        await Run(verify: true, failOnMismatch: false, OutputFormat.Sarif, output, TamperedSkill, DynamicSkill);

        var sarif = await File.ReadAllTextAsync(output);
        sarif.Should().Contain(DiagnosticCodes.McpSkillDigestMismatch);
        sarif.Should().NotContain(DiagnosticCodes.McpSkillDynamicIntegrityLimit);
    }

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    [InlineData(OutputFormat.Sarif)]
    public async Task ACredentialInTheServerUrlReachesNoOutput(string format)
    {
        var output = Path.Combine(_root, $"secret.{format}");

        await Run(verify: true, failOnMismatch: false, format, output, GoodSkill, TamperedSkill);

        (await File.ReadAllTextAsync(output)).Should().NotContain(UrlSecret);
    }

    [Fact]
    public async Task InspectNeverFetchesASkill()
    {
        var client = new StubSkillsClient(GoodSkill);

        var exit = await Run(verify: false, failOnMismatch: false, OutputFormat.Json, Path.Combine(_root, "i.json"), client);

        exit.Should().Be(ExitCodes.Success);
        client.Calls.Should().Equal("skills/list");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RootCommand Root() => SkillForgeCommandLine.Build(CompositionRoot.Build());

    private Task<int> Run(bool verify, bool failOnMismatch, string format, string output, params string[] skills) =>
        Run(verify, failOnMismatch, format, output, new StubSkillsClient(skills));

    private async Task<int> Run(bool verify, bool failOnMismatch, string format, string output, StubSkillsClient client)
    {
        var config = Path.Combine(_root, "mcp.json");
        await File.WriteAllTextAsync(
            config,
            $$"""{ "mcpServers": { "skills": { "url": "https://ci:{{UrlSecret}}@skills.example.test/mcp" } } }""");

        var fileSystem = new FileSystem();
        var files = new McpFileInspector(
            [new JsonMcpConfigurationReader(fileSystem)],
            new McpDeclarationInspector(),
            new McpProber([new StubAdapter()]),
            fileSystem);

        var runner = new McpSkillsCommandRunner(
            new McpSkillsInspector(
                files,
                client,
                new McpSkillVerifier(client, new YamlFrontmatterParser(), new Sha256HashCalculator(), TimeProvider.System)),
            fileSystem,
            [new JsonReportSerializer(), new SarifReportSerializer()]);

        return await runner.RunAsync(new McpSkillsRequest(
            config,
            Probe: true,
            verify,
            [],
            failOnMismatch,
            format,
            output,
            new Application.Abstractions.ReportRenderOptions()));
    }

    /// <summary>A server that answered discovery in 2026-07-28 and declared the skills extension.</summary>
    private sealed class StubAdapter : IMcpProtocolAdapter
    {
        public string ProtocolVersion => "2026-07-28";

        public Task<McpServerProbe> ProbeAsync(McpServerDeclaration server, CancellationToken cancellationToken = default) =>
            Task.FromResult(McpServerProbe.Answered(server.Name, ["2026-07-28"], ["resources"], "stub", "1.0", "2026-07-28") with
            {
                Extensions = [new McpExtensionDeclaration(McpSkillsInspector.ExtensionIdentifier, [])],
            });
    }

    /// <summary>
    /// Serves a fixed set of skills: <c>good</c> matches its manifest, <c>tampered</c> does not, <c>daily</c> is dynamic.
    /// </summary>
    private sealed class StubSkillsClient(params string[] skills) : IMcpSkillsClient
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes("---\nname: x\ndescription: y\n---\n\nbody\n");

        internal List<string> Calls { get; } = [];

        public Task<McpSkillListing> ListAsync(McpServerDeclaration server, CancellationToken cancellationToken = default)
        {
            Calls.Add("skills/list");
            return Task.FromResult(new McpSkillListing([.. skills.Select(Entry)], 1, McpToolPagingOutcome.Complete));
        }

        public Task<McpResponse<McpSkillEntry>> GetAsync(
            McpServerDeclaration server,
            string skillUri,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("skills/get");
            return Task.FromResult(McpResponse<McpSkillEntry>.Ok(Entry(skillUri)));
        }

        public Task<McpResponse<McpResourceContent>> ReadAsync(
            McpServerDeclaration server,
            string resourceUri,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("resources/read");
            var bytes = resourceUri == TamperedSkill ? Encoding.UTF8.GetBytes("---\nname: x\ndescription: z\n---\n\nbody\n") : Content;
            return Task.FromResult(McpResponse<McpResourceContent>.Ok(new McpResourceContent(resourceUri, bytes, "text/markdown")));
        }

        public Task<McpResponse<McpDirectoryListing>> ReadDirectoryAsync(
            McpServerDeclaration server,
            string directoryUri,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("directoryRead was not declared, so this must never be called");

        private static McpSkillEntry Entry(string uri) =>
            new(
                uri,
                """{"name":"x","description":"y"}""",
                uri == DynamicSkill
                    ? McpSkillManifest.Dynamic
                    : McpSkillManifest.Static(
                    [
                        new McpSkillResource(uri, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Content)), Content.Length),
                    ]));
    }
}
