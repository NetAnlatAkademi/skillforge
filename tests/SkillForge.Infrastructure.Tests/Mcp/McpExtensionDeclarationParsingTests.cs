using System.Net;
using System.Text;
using SkillForge.Infrastructure.Mcp;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// How <c>capabilities.extensions</c> is read off a <c>server/discover</c> answer.
/// </summary>
public sealed class McpExtensionDeclarationParsingTests
{
    [Fact]
    public async Task KeepsOnlySettingsThatAreTheLiteralTrue()
    {
        var probe = await new Mcp20260728ProtocolAdapter(new HttpClient(new Discover("""
            {
              "io.modelcontextprotocol/skills": { "directoryRead": "true", "other": false },
              "com.example/x": { "enabled": true }
            }
            """))).ProbeAsync(McpSkillsClientTests.Http());

        probe.Extensions!.Select(extension => extension.Identifier)
            .Should().Equal("com.example/x", "io.modelcontextprotocol/skills");
        probe.Extension("io.modelcontextprotocol/skills")!.EnabledSettings
            .Should().BeEmpty("the string \"true\" is not a boolean, and reading it as one would call an unoffered method");
        probe.Extension("com.example/x")!.Enables("enabled").Should().BeTrue();
    }

    [Fact]
    public async Task AServerWithNoExtensionsDeclaresNone()
    {
        var probe = await new Mcp20260728ProtocolAdapter(new HttpClient(new Discover(null)))
            .ProbeAsync(McpSkillsClientTests.Http());

        probe.Extensions.Should().BeEmpty();
        probe.Extension("io.modelcontextprotocol/skills").Should().BeNull();
    }

    private sealed class Discover(string? extensions) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var declared = extensions is null ? string.Empty : $", \"extensions\": {extensions}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{ "jsonrpc": "2.0", "id": "1", "result": { "supportedVersions": ["2026-07-28"], "capabilities": { "resources": {}{{declared}} } } }""",
                    Encoding.UTF8,
                    "application/json"),
            });
        }
    }
}
