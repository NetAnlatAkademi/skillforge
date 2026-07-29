using System.Net;
using System.Text;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Mcp;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// The handshake-based fallback. Its whole job is to turn "this server does not do discovery" from a dead end into an
/// answer about which revision it does speak.
/// </summary>
public sealed class Mcp20251125ProtocolAdapterTests
{
    [Fact]
    public void ReportsTheRevisionItSpeaks()
    {
        Adapter(Responds("{}")).ProtocolVersion.Should().Be("2025-11-25");
    }

    [Fact]
    public async Task ReadsTheNegotiatedVersionCapabilitiesAndIdentity()
    {
        var adapter = Adapter(Responds("""
            {
              "jsonrpc": "2.0",
              "id": "skillforge-initialize-1",
              "result": {
                "protocolVersion": "2025-11-25",
                "capabilities": { "tools": { "listChanged": true }, "resources": {}, "logging": {} },
                "serverInfo": { "name": "ExampleServer", "version": "1.0.0" }
              }
            }
            """));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Answered);
        probe.AnsweredRevision.Should().Be("2025-11-25");

        // One negotiated version, not a list — the concrete reason the newer revision is preferable.
        probe.SupportedVersions.Should().Equal("2025-11-25");
        probe.Capabilities.Should().Equal("logging", "resources", "tools");
        probe.SelfReportedName.Should().Be("ExampleServer");
    }

    [Fact]
    public async Task SendsTheInitializeRequestTheSpecificationDescribes()
    {
        var handler = Responds("""{ "jsonrpc": "2.0", "id": "1", "result": { "protocolVersion": "2025-11-25" } }""");

        await Adapter(handler).ProbeAsync(Http());

        handler.LastBody.Should().Contain("\"method\":\"initialize\"");
        handler.LastBody.Should().Contain("\"protocolVersion\":\"2025-11-25\"");
        handler.LastBody.Should().Contain("\"clientInfo\"");
    }

    [Fact]
    public async Task DeclaresNoClientCapabilitiesItDoesNotHave()
    {
        // Claiming roots or sampling to get a richer answer would be a lie told to a server.
        var handler = Responds("""{ "jsonrpc": "2.0", "id": "1", "result": { "protocolVersion": "2025-11-25" } }""");

        await Adapter(handler).ProbeAsync(Http());

        handler.LastBody.Should().Contain("\"capabilities\":{}");
    }

    [Fact]
    public async Task SendsNoInitializedNotificationBecauseItIsNotStartingASession()
    {
        var handler = Responds("""{ "jsonrpc": "2.0", "id": "1", "result": { "protocolVersion": "2025-11-25" } }""");

        await Adapter(handler).ProbeAsync(Http());

        handler.Requests.Should().Be(1);
        handler.AllBodies.Should().NotContain(body => body!.Contains("notifications/initialized"));
    }

    [Fact]
    public async Task ReadsAResponseDeliveredAsAServerSentEvent()
    {
        // A 2025-11-25 Streamable HTTP server may answer a POST with an SSE stream rather than plain JSON.
        var adapter = Adapter(Responds(
            "event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":\"1\",\"result\":{\"protocolVersion\":\"2025-11-25\"}}\n\n"));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Answered);
        probe.SupportedVersions.Should().Equal("2025-11-25");
    }

    [Fact]
    public async Task ReportsAFailedInitializeWithItsMessage()
    {
        var adapter = Adapter(Responds("""
            { "jsonrpc": "2.0", "id": "1", "error": { "code": -32602, "message": "Unsupported protocol version" } }
            """));

        var probe = await adapter.ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Unreachable);
        probe.Detail.Should().Contain("Unsupported protocol version");
    }

    [Fact]
    public async Task ReportsA401AsRequiringAuthorization()
    {
        var probe = await Adapter(Responds("nope", HttpStatusCode.Unauthorized)).ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.RequiresAuthorization);
    }

    private static Mcp20251125ProtocolAdapter Adapter(FakeHandler handler) => new(new HttpClient(handler));

    private static McpServerDeclaration Http() =>
        new("remote", "claude-code", McpTransport.Http, "https://mcp.example.test/mcp", [], [], "/home/dev/.claude.json");

    private static FakeHandler Responds(string body, HttpStatusCode status = HttpStatusCode.OK) => new(body, status);

    private sealed class FakeHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        internal int Requests { get; private set; }

        internal string? LastBody { get; private set; }

        internal List<string?> AllBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AllBodies.Add(LastBody);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
