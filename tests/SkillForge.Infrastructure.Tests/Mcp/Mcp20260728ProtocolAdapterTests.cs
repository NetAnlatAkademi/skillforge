using System.Net;
using System.Text;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Mcp;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// The adapter against handlers that answer like MCP servers. The field names asserted here come from the
/// specification's own <c>server/discover</c> example, not from a summary of it.
/// </summary>
public sealed class Mcp20260728ProtocolAdapterTests
{
    private const string Url = "https://mcp.example.test/mcp";

    [Fact]
    public void ReportsTheRevisionItSpeaks()
    {
        Adapter(Responds("{}")).ProtocolVersion.Should().Be("2026-07-28");
    }

    [Fact]
    public async Task ReadsSupportedVersionsCapabilitiesAndSelfReportedIdentity()
    {
        var adapter = Adapter(Responds("""
            {
              "jsonrpc": "2.0",
              "id": "skillforge-discover-1",
              "result": {
                "resultType": "complete",
                "supportedVersions": ["2026-07-28", "2025-11-25"],
                "capabilities": { "tools": {}, "resources": {} },
                "_meta": {
                  "io.modelcontextprotocol/serverInfo": { "name": "ExampleServer", "version": "1.0.0" }
                }
              }
            }
            """));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Answered);
        probe.SupportedVersions.Should().Equal("2026-07-28", "2025-11-25");
        probe.Capabilities.Should().Equal("resources", "tools");
        probe.SelfReportedName.Should().Be("ExampleServer");
        probe.SelfReportedVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task SendsTheDiscoverRequestTheSpecificationDescribes()
    {
        var handler = Responds("""{ "jsonrpc": "2.0", "id": "1", "result": { "supportedVersions": [] } }""");

        await Adapter(handler).ProbeAsync(Http());

        handler.LastBody.Should().Contain("\"method\":\"server/discover\"");
        handler.LastBody.Should().Contain("io.modelcontextprotocol/protocolVersion\":\"2026-07-28\"");
        handler.LastBody.Should().Contain("io.modelcontextprotocol/clientInfo");
        handler.LastHeaders.Should().Contain("MCP-Protocol-Version: 2026-07-28");
        handler.LastMethod.Should().Be("POST");
    }

    [Fact]
    public async Task TreatsMethodNotFoundAsAHandshakeBasedServerRatherThanAFailure()
    {
        // The distinction matters: an older revision is fine and interesting, an unreachable server says nothing.
        var adapter = Adapter(Responds("""
            { "jsonrpc": "2.0", "id": "1", "error": { "code": -32601, "message": "Method not found" } }
            """));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.NoDiscovery);
        probe.Detail.Should().Contain("method not found");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task TreatsAnUnknownRouteAsAHandshakeBasedServerToo(HttpStatusCode status)
    {
        var probe = await Adapter(Responds("not found", status)).ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.NoDiscovery);
    }

    [Fact]
    public async Task ReportsAnotherJsonRpcErrorAsUnreachableWithItsCode()
    {
        var adapter = Adapter(Responds("""
            { "jsonrpc": "2.0", "id": "1", "error": { "code": -32000, "message": "unauthorized" } }
            """));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Unreachable);
        probe.Detail.Should().Contain("-32000").And.Contain("unauthorized");
    }

    [Fact]
    public async Task ReportsAnUnreachableHostWithTheUnderlyingReason()
    {
        var probe = await Adapter(Throws(new HttpRequestException("No such host is known."))).ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Unreachable);
        probe.Detail.Should().Be("No such host is known.");
    }

    [Fact]
    public async Task DoesNotProbeADeclarationWithNoUrl()
    {
        var handler = Responds("{}");
        var server = new McpServerDeclaration(
            "no-url", "claude-code", McpTransport.Http, null, [], [], "/home/dev/.claude.json");

        var probe = await Adapter(handler).ProbeAsync(server);

        probe.Status.Should().Be(McpProbeStatus.NotProbed);
        handler.LastBody.Should().BeNull();
    }

    private static Mcp20260728ProtocolAdapter Adapter(FakeHandler handler) => new(new HttpClient(handler));

    private static McpServerDeclaration Http() =>
        new("remote", "claude-code", McpTransport.Http, Url, [], [], "/home/dev/.claude.json");

    private static FakeHandler Responds(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(body, status, null);

    private static FakeHandler Throws(Exception exception) => new(null, HttpStatusCode.OK, exception);

    private sealed class FakeHandler(string? body, HttpStatusCode status, Exception? throws) : HttpMessageHandler
    {
        internal string? LastBody { get; private set; }

        internal string? LastMethod { get; private set; }

        internal IReadOnlyList<string> LastHeaders { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method.Method;
            LastHeaders = [.. request.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}")];
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (throws is not null)
            {
                throw throws;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? "{}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
