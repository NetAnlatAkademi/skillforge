using System.Net;
using System.Text;
using SkillForge.Application.Discovery;
using SkillForge.Application.Graph;
using SkillForge.Application.Inspection;
using SkillForge.Application.Mcp;
using SkillForge.Application.Policy;
using SkillForge.Application.Skills;
using SkillForge.Cli.Commands;
using SkillForge.Domain.Discovery;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Discovery;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Provenance;
using SkillForge.Infrastructure.Yaml;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// One place that asserts no secret reaches any output, across every command that could carry one.
/// </summary>
/// <remarks>
/// Spread across the individual command tests, this property is easy to leave out of the next command. Here it is
/// a single list of secrets and a single list of outputs, so adding a command means adding one line — and
/// forgetting to is visible.
///
/// The four shapes a secret arrives in: an environment variable value, an `Authorization` header value, a
/// credential inside a URL's user-info, and an API key in a registry listing. The first two never enter the model
/// at all; the third is redacted where the URL is read; the fourth is preserved as a *name* only.
/// </remarks>
public sealed class SecretSafetyTests : IDisposable
{
    private const string EnvironmentSecret = "sk-live-environment-must-not-print";
    private const string HeaderSecret = "Bearer sk-live-header-must-not-print";
    private const string UrlSecret = "sk-live-url-must-not-print";

    private static readonly string[] Secrets = [EnvironmentSecret, HeaderSecret, UrlSecret];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-secret-tests",
        Guid.NewGuid().ToString("n"));

    public SecretSafetyTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    public async Task McpInspectPrintsNoSecret(string format)
    {
        WriteMcp();

        var output = Path.Combine(_root, "mcp.out");

        await McpRunner().InspectAsync(
            new McpRequest(McpPath, Probe: false, Gate: false, format, output, Render()),
            CancellationToken.None);

        await AssertClean(output);
    }

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    public async Task McpSurfacePrintsNoSecret(string format)
    {
        WriteMcp();

        var output = Path.Combine(_root, "surface.out");

        await McpRunner().SurfaceAsync(
            new McpSurfaceRequest(
                McpPath,
                Probe: false,
                Path.Combine(_root, "no-policy.yaml"),
                FailOnThreshold: false,
                format,
                output,
                Render()),
            CancellationToken.None);

        await AssertClean(output);
    }

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    [InlineData(OutputFormat.Mermaid)]
    public async Task GraphPrintsNoSecret(string format)
    {
        WriteMcp();

        var output = Path.Combine(_root, "graph.out");

        await GraphRunner().RunAsync(new GraphRequest(_root, format, output, Render()), CancellationToken.None);

        await AssertClean(output);
    }

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    public async Task DiscoverPrintsNoSecret(string format)
    {
        var output = Path.Combine(_root, "discover.out");

        await DiscoverRunner().RunAsync(
            new DiscoverRequest(
                "q",
                "https://registry.example.test/resources",
                ArdDiscoveryAdapter.AdapterKind,
                null,
                Limits,
                format,
                output,
                Render()),
            CancellationToken.None);

        await AssertClean(output);
    }

    [Theory]
    [InlineData(OutputFormat.Console)]
    [InlineData(OutputFormat.Json)]
    [InlineData(OutputFormat.Sarif)]
    public async Task DiscoveryVerifyPrintsNoSecret(string format)
    {
        var output = Path.Combine(_root, "verify.out");

        await VerifyRunner().RunAsync(
            new DiscoveryVerifyRequest(
                "q",
                "https://registry.example.test/resources",
                ArdDiscoveryAdapter.AdapterKind,
                Probe: true,
                FailOnDrift: false,
                Limits,
                format,
                output,
                Render()),
            CancellationToken.None);

        await AssertClean(output);
    }

    [Fact]
    public void ACredentialInAUrlIsStrippedWhereTheUrlIsRead()
    {
        // Redaction at the reader rather than at the printer, because there are eight printers and one reader.
        UrlRedaction.WithoutCredentials($"https://svc:{UrlSecret}@db.example.test/mcp")
            .Should().Be("https://db.example.test/mcp");

        UrlRedaction.WithoutCredentials(new Uri($"https://svc:{UrlSecret}@db.example.test/mcp"))
            .Should().Be(new Uri("https://db.example.test/mcp"));
    }

    [Fact]
    public void ACommandLineIsNotAUrlAndSurvivesRedactionUnchanged()
    {
        // The same field on a declaration holds either one, so `npx -y some-mcp` must come through untouched.
        UrlRedaction.WithoutCredentials("npx -y some-mcp").Should().Be("npx -y some-mcp");
        UrlRedaction.WithoutCredentials("https://db.example.test/mcp").Should().Be("https://db.example.test/mcp");
        UrlRedaction.WithoutCredentials((string?)null).Should().BeNull();
    }

    [Fact]
    public async Task TheNamesAreStillReportedBecauseThatIsThePoint()
    {
        // A test that asserted only absence would pass on a report that printed nothing at all.
        WriteMcp();

        var output = Path.Combine(_root, "names.out");

        await GraphRunner().RunAsync(
            new GraphRequest(_root, OutputFormat.Console, output, Render()),
            CancellationToken.None);

        var report = await File.ReadAllTextAsync(output);

        report.Should().Contain("DEPLOY_TOKEN");
        report.Should().Contain("Authorization");
        report.Should().Contain("db.example.test");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task AssertClean(string outputPath)
    {
        var content = await File.ReadAllTextAsync(outputPath);

        content.Should().NotBeEmpty("an empty report would pass every absence assertion below");

        foreach (var secret in Secrets)
        {
            content.Should().NotContain(secret);
        }

        // The distinctive part of each secret, in case a formatter split or escaped the whole string.
        content.Should().NotContain("sk-live");
    }

    private string McpPath => Path.Combine(_root, ".mcp.json");

    private void WriteMcp()
    {
        File.WriteAllText(
            McpPath,
            $$"""
            {
              "mcpServers": {
                "database-tools": {
                  "type": "http",
                  "url": "https://svc:{{UrlSecret}}@db.example.test/mcp",
                  "headers": { "Authorization": "{{HeaderSecret}}" },
                  "env": { "DEPLOY_TOKEN": "{{EnvironmentSecret}}" }
                }
              }
            }
            """);
    }

    private static McpCommandRunner McpRunner()
    {
        var fileSystem = new FileSystem();

        return new McpCommandRunner(
            new McpFileInspector(
                [new JsonMcpConfigurationReader(fileSystem)],
                new McpDeclarationInspector(),
                new McpProber([]),
                fileSystem),
            fileSystem,
            new YamlPolicyReader(fileSystem));
    }

    private static GraphCommandRunner GraphRunner()
    {
        var fileSystem = new FileSystem();

        return new GraphCommandRunner(
            new GraphBuilder(
                fileSystem,
                new SkillDiscovery(fileSystem),
                new SkillLoader(
                    fileSystem,
                    new YamlFrontmatterParser(),
                    new YamlSkillConfigurationReader(fileSystem)),
                new SkillInspector(),
                [new JsonMcpConfigurationReader(fileSystem)],
                new JsonDistributionManifestReader(fileSystem)),
            fileSystem);
    }

    private static DiscoverCommandRunner DiscoverRunner() =>
        new([new ArdDiscoveryAdapter(new HttpClient(new LeakyRegistry()))], new FileSystem());

    private static DiscoveryVerifyCommandRunner VerifyRunner() =>
        new(
            [new ArdDiscoveryAdapter(new HttpClient(new LeakyRegistry()))],
            new DiscoveryVerifier(new McpProber([new LeakyProtocolAdapter()])),
            new FileSystem(),
            [new JsonReportSerializer(), new SarifReportSerializer()]);

    private static RemoteDiscoveryLimits Limits => RemoteDiscoveryLimits.Default with
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private static Application.Abstractions.ReportRenderOptions Render() => new();

    /// <summary>A registry that puts a credential in a URL and an API key in its metadata.</summary>
    private sealed class LeakyRegistry : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "resources": [
                        {
                          "id": "database-tools",
                          "name": "database-tools",
                          "type": "mcp",
                          "endpoint": "https://svc:{{UrlSecret}}@db.example.test/mcp",
                          "apiKey": "{{EnvironmentSecret}}",
                          "authorization": "{{HeaderSecret}}",
                          "tools": ["query"]
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });
    }

    /// <summary>A server that answers with a drifting tool, so the finding path is exercised too.</summary>
    private sealed class LeakyProtocolAdapter : IMcpProtocolAdapter
    {
        public string ProtocolVersion => "2026-07-28";

        public Task<McpServerProbe> ProbeAsync(
            McpServerDeclaration server,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(McpServerProbe.Answered(
                server.Name,
                ["2026-07-28"],
                ["tools"],
                "StubServer",
                null,
                "2026-07-28",
                [new McpToolSummary("query", true, null, []), new McpToolSummary("drop_table", true, null, [])],
                new McpToolPaging(1, 2, McpToolPagingOutcome.Complete)));
    }
}
