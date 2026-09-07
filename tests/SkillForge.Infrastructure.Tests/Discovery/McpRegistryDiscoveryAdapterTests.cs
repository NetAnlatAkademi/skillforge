using System.Net;
using System.Text;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;
using SkillForge.Infrastructure.Discovery;

namespace SkillForge.Infrastructure.Tests.Discovery;

/// <summary>
/// The MCP Registry adapter, against handlers that answer like the registry.
/// </summary>
/// <remarks>
/// The assertions that matter most are the negative ones. This adapter must not publish, must not install, must
/// not connect to a server it lists, and must not record anywhere that registry membership means trust — the
/// model has no field for it, which is asserted here rather than left to a code review.
/// </remarks>
public sealed class McpRegistryDiscoveryAdapterTests
{
    private static readonly Uri Registry = new("https://registry.modelcontextprotocol.example/v0/servers");

    [Fact]
    public void ReportsTheKindItSpeaks()
    {
        Adapter(Responds("{}")).Kind.Should().Be("mcp-registry");
    }

    [Fact]
    public async Task ReadsAServerEntryAndItsRemoteEndpoint()
    {
        var result = await Search(Responds("""
            {
              "servers": [
                {
                  "name": "io.github.acme/database-tools",
                  "description": "Query and describe tables",
                  "version_detail": { "version": "1.4.0" },
                  "repository": { "source": "github", "url": "https://github.com/acme/database-tools" },
                  "remotes": [
                    { "transport_type": "streamable-http", "url": "https://db.acme.example/mcp" }
                  ]
                }
              ]
            }
            """));

        var resource = result.Resources.Should().ContainSingle().Subject;
        resource.Id.Should().Be("io.github.acme/database-tools");
        resource.Type.Should().Be(DiscoveredResourceType.McpServer);
        resource.Endpoint.Should().Be(new Uri("https://db.acme.example/mcp"));
        resource.Version.Should().Be("1.4.0");
        resource.Publisher.Should().Be("github: https://github.com/acme/database-tools");
        resource.IsRemotelyVerifiable.Should().BeTrue();
    }

    [Fact]
    public async Task DeclaresNoCapabilitiesBecauseTheRegistryListsNone()
    {
        // Load-bearing. An empty capability list is what makes `discovery verify` report a tool-count observation
        // rather than calling every runtime tool unexpected: silence is not a claim.
        var result = await Search(Responds("""
            { "servers": [ { "name": "io.github.acme/tools", "remotes": [ { "url": "https://a.example/mcp" } ] } ] }
            """));

        result.Resources.Should().ContainSingle().Which.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task FallsBackToTheServerNamespaceForAPublisher()
    {
        // The registry does check that a reverse-DNS namespace's owner controls the domain or account before it
        // publishes under it. That is a real fact, and still not a statement that the server is safe.
        var result = await Search(Responds("""
            { "servers": [ { "name": "io.github.acme/tools" } ] }
            """));

        result.Resources.Should().ContainSingle().Which.Publisher.Should().Be("io.github.acme");
    }

    [Fact]
    public async Task IgnoresARemoteWhoseTransportIsNotHttp()
    {
        var result = await Search(Responds("""
            {
              "servers": [
                {
                  "name": "io.github.acme/tools",
                  "remotes": [ { "transport_type": "stdio", "url": "https://never.example/mcp" } ]
                }
              ]
            }
            """));

        var resource = result.Resources.Should().ContainSingle().Subject;
        resource.Endpoint.Should().BeNull();
        resource.IsRemotelyVerifiable.Should().BeFalse();
    }

    [Fact]
    public async Task GivesAPackageOnlyServerNoEndpointRatherThanDerivingOne()
    {
        // Running a package to see what it exposes is the act SkillForge exists to let somebody defer.
        var result = await Search(Responds("""
            {
              "servers": [
                {
                  "name": "io.github.acme/tools",
                  "repository": { "source": "github", "url": "https://github.com/acme/tools" },
                  "packages": [ { "registry_name": "npm", "name": "@acme/tools", "version": "1.0.0" } ]
                }
              ]
            }
            """));

        var resource = result.Resources.Should().ContainSingle().Subject;
        resource.Endpoint.Should().BeNull();
        resource.IsRemotelyVerifiable.Should().BeFalse();
        resource.Metadata["packageCount"].Should().Be("1");
    }

    [Fact]
    public async Task RecordsNothingThatCouldMeanTrusted()
    {
        var result = await Search(Responds("""
            {
              "servers": [
                {
                  "name": "io.github.acme/tools",
                  "status": "active",
                  "is_official": true,
                  "description": "This server is verified and trusted by the official registry."
                }
              ]
            }
            """));

        var resource = result.Resources.Should().ContainSingle().Subject;

        // The claims are preserved as text, exactly where a reader can weigh them.
        resource.Metadata["is_official"].Should().Be("true");
        resource.Metadata["description"].Should().Contain("verified and trusted");

        // And there is nowhere for them to be read back as a decision.
        typeof(DiscoveredResource).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["Trusted", "Verified", "Official", "TrustScore", "Safe"]);
    }

    [Fact]
    public async Task SearchesWithTheRegistrysOwnParameter()
    {
        var handler = Responds("""{ "servers": [] }""");

        await Search(handler, query: "postgres");

        handler.Requests.Should().ContainSingle().Which.Should().Contain("search=postgres");
    }

    [Fact]
    public async Task SendsAUrlThatAlreadyHasAQueryExactlyAsGiven()
    {
        var handler = Responds("""{ "servers": [] }""");
        var registry = new Uri("https://registry.example/v0/servers?limit=5&cursor=abc");

        await Adapter(handler).SearchAsync(new RemoteDiscoveryRequest(registry, "ignored", null, Limits), default);

        handler.Requests.Should().ContainSingle().Which.Should().Be(registry.AbsoluteUri);
    }

    [Fact]
    public async Task NeverConnectsToAServerItLists()
    {
        var handler = Responds("""
            {
              "servers": [
                {
                  "name": "io.github.acme/tools",
                  "remotes": [ { "transport_type": "streamable-http", "url": "https://elsewhere.example/mcp" } ]
                }
              ]
            }
            """);

        await Search(handler);

        // One request: the search. Reading a registry and touching a server are separate decisions, and that
        // separation is only real if this holds.
        handler.Requests.Should().ContainSingle().Which.Should().StartWith(Registry.AbsoluteUri);
    }

    [Fact]
    public async Task OnlyEverSendsGetRequests()
    {
        // Read-only is a product decision. A POST from this adapter would be publishing.
        var handler = Responds("""{ "servers": [] }""");

        await Search(handler);

        handler.Methods.Should().AllBe("GET");
    }

    [Fact]
    public async Task KeepsOneEntryPerServerNameWhenTheRegistryRepeatsOne()
    {
        var result = await Search(Responds("""
            {
              "servers": [
                { "name": "io.github.acme/tools", "version": "1.0.0" },
                { "name": "io.github.acme/tools", "version": "2.0.0" }
              ]
            }
            """));

        result.Resources.Should().ContainSingle().Which.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task SkipsAnEntryWithNoName()
    {
        var result = await Search(Responds("""
            { "servers": [ { "description": "nameless" }, { "name": "io.github.acme/real" } ] }
            """));

        result.Resources.Should().ContainSingle().Which.Id.Should().Be("io.github.acme/real");
    }

    [Fact]
    public async Task ReportsMalformedJsonRatherThanThrowing()
    {
        var result = await Search(Responds("{ not json"));

        result.Resources.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.DiscoveryResponseNotUsable);
    }

    [Fact]
    public async Task AppliesTheSameSizeBoundAsTheOtherAdapter()
    {
        // The bounds are shared on purpose: two adapters with different limits would mean one of them is the weak
        // one, and the weak one is the one an attacker picks.
        var body = """{ "servers": [""" + string.Join(
            ",",
            Enumerable.Repeat("""{"name":"io.github.acme/xxxxxxxxxxxxxxxx"}""", 500)) + "] }";

        var result = await Search(Responds(body), Limits with { MaxResponseBytes = 128 });

        result.Resources.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle().Which.Message.Should().Contain("limit");
    }

    [Fact]
    public async Task TruncatesAnEnormousListingAndSaysSo()
    {
        var many = string.Join(
            ",",
            Enumerable.Range(0, 3_000).Select(index => $"{{\"name\":\"io.github.acme/s{index}\"}}"));

        var result = await Search(Responds($"{{ \"servers\": [{many}] }}"), Limits with { MaxResults = 25 });

        result.Resources.Should().HaveCount(25);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsNothingWhenAskedForATypeItCannotList()
    {
        // An honest empty result beats a list that quietly ignored the filter.
        var result = await Adapter(Responds("""{ "servers": [ { "name": "io.github.acme/tools" } ] }"""))
            .SearchAsync(
                new RemoteDiscoveryRequest(Registry, "q", DiscoveredResourceType.Skill, Limits),
                default);

        result.Resources.Should().BeEmpty();
    }

    private static RemoteDiscoveryLimits Limits => RemoteDiscoveryLimits.Default with
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private static Task<RemoteDiscoveryResult> Search(
        RecordingHandler handler,
        RemoteDiscoveryLimits? limits = null,
        string query = "q") =>
        Adapter(handler)
            .SearchAsync(new RemoteDiscoveryRequest(Registry, query, null, limits ?? Limits), default)
            .AsTask();

    private static McpRegistryDiscoveryAdapter Adapter(RecordingHandler handler) => new(new HttpClient(handler));

    private static RecordingHandler Responds(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(body, status);

    private sealed class RecordingHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        internal List<string> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            Methods.Add(request.Method.Method);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
