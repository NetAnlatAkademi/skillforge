using System.Net;
using System.Text;
using SkillForge.Application.Discovery;
using SkillForge.Application.Mcp;
using SkillForge.Cli.Commands;
using SkillForge.Domain.Discovery;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Discovery;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>discover</c> and <c>discovery verify</c>, with the registry and the MCP server both under test control.
/// </summary>
/// <remarks>
/// The assertions that matter are about restraint: no registry means no request, no <c>--probe</c> means no
/// server is contacted, and a registry's own words never reach a report as SkillForge's conclusion.
/// </remarks>
public sealed class DiscoverCommandRunnerTests : IDisposable
{
    private const string RegistryUrl = "https://registry.example.test/resources";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-discover-tests",
        Guid.NewGuid().ToString("n"));

    public DiscoverCommandRunnerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task NoRegistryIsAUsageErrorAndMakesNoRequest()
    {
        var handler = new StubRegistry("[]");

        var exit = await RunDiscover(handler, registryUrl: string.Empty);

        exit.Should().Be(2);
        handler.Requests.Should().BeEmpty("a discovery command must never reach a registry nobody named");
    }

    [Fact]
    public async Task ANonHttpRegistryIsAUsageError()
    {
        (await RunDiscover(new StubRegistry("[]"), registryUrl: "file:///etc/passwd")).Should().Be(2);
        (await RunDiscover(new StubRegistry("[]"), registryUrl: "not a url")).Should().Be(2);
    }

    [Fact]
    public async Task AnUnknownAdapterKindIsAUsageError()
    {
        (await RunDiscover(new StubRegistry("[]"), kind: "made-up")).Should().Be(2);
    }

    [Fact]
    public async Task ReportsWhatTheRegistryDeclaredAndSaysThatIsWhatItIs()
    {
        var report = await Discover(new StubRegistry("""
            {
              "resources": [
                {
                  "id": "urn:database-tools",
                  "name": "database-tools",
                  "type": "mcp",
                  "endpoint": "https://db.example.test/mcp",
                  "publisher": "Example Inc",
                  "tools": ["query", "delete_table"]
                }
              ]
            }
            """));

        report.Should().Contain("DECLARED BY THE REGISTRY. None of it has been verified.");
        report.Should().Contain("publisher:    Example Inc (declared, unverified)");
        report.Should().Contain("capabilities: 2 declared");
        report.Should().Contain("verifiable:   yes — remote HTTP MCP server");

        // The sentence the whole product thesis rests on.
        report.Should().Contain("It is not evidence that it is safe, trusted");
    }

    [Fact]
    public async Task ARegistrysClaimToBeVerifiedIsPreservedAsMetadataAndNotAsAVerdict()
    {
        var report = await Discover(
            new StubRegistry("""
                {
                  "resources": [
                    {
                      "id": "one",
                      "name": "one",
                      "type": "mcp",
                      "verified": true,
                      "trust_level": "official"
                    }
                  ]
                }
                """),
            format: OutputFormat.Json);

        report.Should().Contain("\"verified\": \"true\"", "preserved, as text, under metadata");
        report.Should().Contain("\"declaredByRegistry\": true");

        // No top-level verdict field anywhere for it to have set.
        report.Should().NotContain("\"trusted\":");
        report.Should().NotContain("\"safe\":");
    }

    [Fact]
    public async Task ARegistryThatCannotBeSearchedFailsTheRun()
    {
        var exit = await RunDiscover(new StubRegistry("nope", HttpStatusCode.BadGateway));

        exit.Should().Be(1, "'no results' and 'no answer' are different facts");
    }

    [Fact]
    public async Task VerifyContactsNothingWithoutProbe()
    {
        var registry = new StubRegistry(OneServer);
        var server = new StubMcpServer(["query", "delete_database"]);

        var report = await Verify(registry, server, probe: false);

        server.Requests.Should().BeEmpty();
        report.Should().Contain("Probed:    0");
        report.Should().Contain("Add --probe for that");
    }

    [Fact]
    public async Task VerifyReportsARuntimeToolTheRegistryNeverDeclared()
    {
        var registry = new StubRegistry(OneServer);
        var server = new StubMcpServer(["query", "delete_database"]);

        var report = await Verify(registry, server, probe: true);

        report.Should().Contain("drift detected");
        report.Should().Contain("UnexpectedRuntimeTool — delete_database");
        report.Should().Contain("SF8201");

        // Both halves of the claim, and where each was read.
        report.Should().Contain($"read from {RegistryUrl} and https://db.example.test/mcp");
    }

    [Fact]
    public async Task VerifyNeverCallsANoDriftResultTrustedOrSafe()
    {
        var registry = new StubRegistry(OneServer);
        var server = new StubMcpServer(["query"]);

        var report = await Verify(registry, server, probe: true);

        report.Should().Contain("verified, no drift — not a statement about trust or safety");
        report.Should().Contain("It does NOT mean trusted, and it does NOT mean safe.");
    }

    [Fact]
    public async Task VerifyFailsOnDriftOnlyWhenAsked()
    {
        (await RunVerify(new StubRegistry(OneServer), new StubMcpServer(["query", "extra"]), probe: true))
            .Should().Be(0);

        (await RunVerify(
                new StubRegistry(OneServer),
                new StubMcpServer(["query", "extra"]),
                probe: true,
                failOnDrift: true))
            .Should().Be(1);
    }

    [Fact]
    public async Task VerifyEmitsSarifForTheFindingsAlone()
    {
        var report = await Verify(
            new StubRegistry(OneServer),
            new StubMcpServer(["query", "delete_database"]),
            probe: true,
            format: OutputFormat.Sarif);

        report.Should().Contain("\"$schema\"").And.Contain("sarif");
        report.Should().Contain("SF8201");

        // The listing itself is a search result rather than a scanner finding, so it is not in the upload.
        report.Should().NotContain("declaredCapabilities");
    }

    [Fact]
    public async Task VerifyNeverProbesAStdioResource()
    {
        var registry = new StubRegistry("""
            {
              "resources": [
                { "id": "local", "name": "local", "type": "mcp", "endpoint": "file:///usr/local/bin/server" }
              ]
            }
            """);

        var server = new StubMcpServer(["query"]);

        var report = await Verify(registry, server, probe: true);

        server.Requests.Should().BeEmpty();
        report.Should().Contain("not verifiable");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private const string OneServer = """
        {
          "resources": [
            {
              "id": "database-tools",
              "name": "database-tools",
              "type": "mcp",
              "endpoint": "https://db.example.test/mcp",
              "tools": ["query"]
            }
          ]
        }
        """;

    private async Task<string> Discover(StubRegistry registry, string format = OutputFormat.Console)
    {
        var output = Path.Combine(_root, "discover.out");

        await RunDiscover(registry, format: format, output: output);

        return await File.ReadAllTextAsync(output);
    }

    private static async Task<int> RunDiscover(
        StubRegistry registry,
        string registryUrl = RegistryUrl,
        string kind = ArdDiscoveryAdapter.AdapterKind,
        string format = OutputFormat.Console,
        string? output = null)
    {
        var runner = new DiscoverCommandRunner(
            [new ArdDiscoveryAdapter(new HttpClient(registry))],
            new FileSystem());

        return await runner.RunAsync(
            new DiscoverRequest(
                "q",
                registryUrl,
                kind,
                null,
                Limits,
                format,
                output,
                new Application.Abstractions.ReportRenderOptions()),
            CancellationToken.None);
    }

    private async Task<string> Verify(
        StubRegistry registry,
        StubMcpServer server,
        bool probe,
        string format = OutputFormat.Console)
    {
        var output = Path.Combine(_root, "verify.out");

        await RunVerify(registry, server, probe, output: output, format: format);

        return await File.ReadAllTextAsync(output);
    }

    private static async Task<int> RunVerify(
        StubRegistry registry,
        StubMcpServer server,
        bool probe,
        bool failOnDrift = false,
        string format = OutputFormat.Console,
        string? output = null)
    {
        var runner = new DiscoveryVerifyCommandRunner(
            [new ArdDiscoveryAdapter(new HttpClient(registry))],
            new DiscoveryVerifier(new McpProber([new StubProtocolAdapter(server)])),
            new FileSystem(),
            [new JsonReportSerializer(), new SarifReportSerializer()]);

        return await runner.RunAsync(
            new DiscoveryVerifyRequest(
                "q",
                RegistryUrl,
                ArdDiscoveryAdapter.AdapterKind,
                probe,
                failOnDrift,
                Limits,
                format,
                output,
                new Application.Abstractions.ReportRenderOptions()),
            CancellationToken.None);
    }

    private static RemoteDiscoveryLimits Limits => RemoteDiscoveryLimits.Default with
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private sealed class StubRegistry(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>Records what a discovered server was asked, and answers with a fixed tool list.</summary>
    private sealed class StubMcpServer(IReadOnlyList<string> tools)
    {
        internal List<string> Requests { get; } = [];

        internal IReadOnlyList<string> Tools => tools;
    }

    private sealed class StubProtocolAdapter(StubMcpServer server) : IMcpProtocolAdapter
    {
        public string ProtocolVersion => "2026-07-28";

        public Task<McpServerProbe> ProbeAsync(
            McpServerDeclaration declaration,
            CancellationToken cancellationToken = default)
        {
            server.Requests.Add(declaration.Command ?? string.Empty);

            return Task.FromResult(McpServerProbe.Answered(
                declaration.Name,
                ["2026-07-28"],
                ["tools"],
                "StubServer",
                null,
                "2026-07-28",
                [.. server.Tools.Select(name => new McpToolSummary(name, true, null, []))],
                new McpToolPaging(1, server.Tools.Count, McpToolPagingOutcome.Complete)));
        }
    }
}
