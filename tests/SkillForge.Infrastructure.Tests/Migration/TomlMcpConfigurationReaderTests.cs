using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Migration;

namespace SkillForge.Infrastructure.Tests.Migration;

public sealed class TomlMcpConfigurationReaderTests : IDisposable
{
    private readonly TomlMcpConfigurationReader _reader = new(new FileSystem());
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-mcp-toml-tests",
        Guid.NewGuid().ToString("n"));

    public TomlMcpConfigurationReaderTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("config.toml", true)]
    [InlineData("mcp.json", false)]
    public void ClaimsTheTomlFiles(string fileName, bool expected)
    {
        _reader.CanRead(Path.Combine(_root, fileName)).Should().Be(expected);
    }

    [Fact]
    public async Task ReadsTheFormsARealCodexConfigurationUses()
    {
        // Literal strings, an empty array, a key that is not part of the MCP model, and a nested env table — the
        // shapes found in a working ~/.codex/config.toml, which is why this is not a hand-written scanner.
        var path = Write("""
            model = "gpt-5"

            [mcp_servers.node_repl]
            args = []
            command = 'C:\Users\someone\bin\node_repl.exe'
            startup_timeout_sec = 120

            [mcp_servers.node_repl.env]
            NODE_REPL_NODE_PATH = 'C:\Users\someone\bin\node.exe'
            SECRET_TOKEN = "super-secret-value"
            """);

        var result = await _reader.ReadAsync(path, "codex");

        var server = result.Servers.Should().ContainSingle().Subject;
        server.Name.Should().Be("node_repl");
        server.ProviderId.Should().Be("codex");
        server.Transport.Should().Be(McpTransport.Stdio);
        server.Command.Should().EndWith("node_repl.exe");
        server.Arguments.Should().BeEmpty();
        server.EnvironmentVariableNames.Should().Equal("NODE_REPL_NODE_PATH", "SECRET_TOKEN");
        server.ToString().Should().NotContain("super-secret-value", "values are never read");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadsArgumentsAndOrdersServersByName()
    {
        var path = Write("""
            [mcp_servers.zeta]
            command = "node"
            args = ["one", "two"]

            [mcp_servers.alpha]
            command = "node"
            """);

        var result = await _reader.ReadAsync(path, "codex");

        result.Servers.Select(server => server.Name).Should().Equal("alpha", "zeta");
        result.Servers[^1].Arguments.Should().Equal("one", "two");
    }

    [Fact]
    public async Task AConfigurationWithoutTheSectionDeclaresNothingAndIsNotAnError()
    {
        var result = await _reader.ReadAsync(Write("""model = "gpt-5" """), "codex");

        result.Servers.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsSF1015ForAFileItCannotParse()
    {
        var result = await _reader.ReadAsync(Write("[mcp_servers.broken\ncommand ="), "codex");

        result.Servers.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    private string Write(string content)
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():n}.toml");
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
