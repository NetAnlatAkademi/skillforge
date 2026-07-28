using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Migration;

namespace SkillForge.Infrastructure.Tests.Migration;

public sealed class JsonMcpConfigurationReaderTests : IDisposable
{
    private readonly JsonMcpConfigurationReader _reader = new(new FileSystem());
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-mcp-json-tests",
        Guid.NewGuid().ToString("n"));

    public JsonMcpConfigurationReaderTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("mcp.json", true)]
    [InlineData(".claude.json", true)]
    [InlineData("config.toml", false)]
    public void ClaimsTheJsonFiles(string fileName, bool expected)
    {
        _reader.CanRead(Path.Combine(_root, fileName)).Should().Be(expected);
    }

    [Fact]
    public async Task ReadsAServerWithItsCommandAndArguments()
    {
        var path = Write("""
            {
              "mcpServers": {
                "obsidian": { "type": "stdio", "command": "npx", "args": ["-y", "obsidian-mcp"] }
              }
            }
            """);

        var result = await _reader.ReadAsync(path, "claude-code");

        var server = result.Servers.Should().ContainSingle().Subject;
        server.Name.Should().Be("obsidian");
        server.ProviderId.Should().Be("claude-code");
        server.Transport.Should().Be(McpTransport.Stdio);
        server.Command.Should().Be("npx");
        server.Arguments.Should().Equal("-y", "obsidian-mcp");
        server.SourcePath.Should().Be(path);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadsEnvironmentVariableNamesAndNeverTheirValues()
    {
        // The guarantee the whole command rests on: an MCP declaration is one of the likeliest places in a home
        // directory to hold a token, so the value must not be anywhere in the model.
        var path = Write("""
            {
              "mcpServers": {
                "azure": {
                  "command": "npx",
                  "env": { "AZURE_TOKEN": "super-secret-value", "AZURE_ORG": "contoso" }
                }
              }
            }
            """);

        var result = await _reader.ReadAsync(path, "claude-code");

        var server = result.Servers.Should().ContainSingle().Subject;
        server.EnvironmentVariableNames.Should().Equal("AZURE_ORG", "AZURE_TOKEN");
        server.ToString().Should().NotContain("super-secret-value");
    }

    [Fact]
    public async Task ToleratesCommentsAndTrailingCommas()
    {
        // Not tidiness: ~/.copilot/config.json on a real machine starts with two '//' lines, and a strict parser
        // would report a working configuration as corrupt.
        var path = Write("""
            // User settings belong in settings.json.
            {
              "mcpServers": {
                "local": { "command": "node", },
              },
            }
            """);

        var result = await _reader.ReadAsync(path, "github-copilot");

        result.Servers.Should().ContainSingle().Which.Name.Should().Be("local");
        result.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "mcpServers": { "s": { "url": "https://example.test/mcp" } } }""", McpTransport.Http)]
    [InlineData("""{ "mcpServers": { "s": { "type": "sse", "url": "https://e.test" } } }""", McpTransport.Http)]
    [InlineData("""{ "mcpServers": { "s": { "command": "node" } } }""", McpTransport.Stdio)]
    [InlineData("""{ "mcpServers": { "s": { } } }""", McpTransport.Unknown)]
    public async Task ReadsTheTransportFromWhatTheFileSaysBeforeInferringIt(string json, McpTransport expected)
    {
        var result = await _reader.ReadAsync(Write(json), "cursor");

        result.Servers.Should().ContainSingle().Which.Transport.Should().Be(expected);
    }

    [Fact]
    public async Task OrdersServersByNameSoAReportIsReproducible()
    {
        var path = Write("""{ "mcpServers": { "zeta": {}, "alpha": {}, "mid": {} } }""");

        var result = await _reader.ReadAsync(path, "claude-code");

        result.Servers.Select(server => server.Name).Should().Equal("alpha", "mid", "zeta");
    }

    [Fact]
    public async Task AFileWithNoMcpSectionDeclaresNothingAndIsNotAnError()
    {
        // ~/.claude.json holds a great deal that is not MCP configuration.
        var result = await _reader.ReadAsync(Write("""{ "theme": "dark" }"""), "claude-code");

        result.Servers.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsSF1015ForAFileItCannotParseRatherThanSkippingItSilently()
    {
        var result = await _reader.ReadAsync(Write("{ this is not json"), "claude-code");

        result.Servers.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("missing from this inventory");
    }

    private string Write(string content)
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():n}.json");
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
