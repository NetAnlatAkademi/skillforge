using SkillForge.Application.Mcp;
using SkillForge.Application.Migration;
using SkillForge.Domain.Diagnostics;
using SkillForge.Infrastructure.Migration;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// Inspecting a file the caller named, with the real readers behind it. Probing is never exercised here: nothing
/// in these tests is allowed to leave the machine, which is also the guarantee the command makes.
/// </summary>
public sealed class McpFileInspectorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-mcp-file-tests",
        Guid.NewGuid().ToString("n"));

    public McpFileInspectorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadsTheServersAJsonFileDeclares()
    {
        var path = Write("mcp.json", """
            {
              "mcpServers": {
                "catalog": { "url": "https://catalog.example/mcp" },
                "obsidian": { "command": "npx", "args": ["-y", "obsidian-mcp"] }
              }
            }
            """);

        var inspection = await Inspector().InspectAsync(path);

        inspection.Servers.Select(server => server.Name).Should().BeEquivalentTo("catalog", "obsidian");
        inspection.Probes.Should().BeEmpty("nothing is probed unless the caller asks");
    }

    [Fact]
    public async Task ReportsWhatTheDeclarationsRevealWithoutConnecting()
    {
        var path = Write("mcp.json", """
            {
              "mcpServers": {
                "legacy": { "url": "http://10.0.0.5/sse" },
                "obsidian": { "command": "npx", "args": ["-y", "obsidian-mcp"] }
              }
            }
            """);

        var inspection = await Inspector().InspectAsync(path);

        inspection.Diagnostics.Select(finding => finding.Code).Should().Contain(
        [
            DiagnosticCodes.McpDeprecatedTransport,
            DiagnosticCodes.McpPlaintextEndpoint,
            DiagnosticCodes.McpServerCommandNotPinned,
        ]);
    }

    [Fact]
    public async Task AFileThatDoesNotExistIsReportedRatherThanReadAsEmpty()
    {
        var inspection = await Inspector().InspectAsync(Path.Combine(_root, "absent.json"));

        inspection.Servers.Should().BeEmpty();
        inspection.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    [Fact]
    public async Task AFormatNoReaderHandlesIsReportedRatherThanGuessedAt()
    {
        var path = Write("mcp.yaml", "mcpServers: {}");

        var inspection = await Inspector().InspectAsync(path);

        inspection.Diagnostics.Should().ContainSingle()
            .Which.Message.Should().Contain(".json and .toml");
    }

    [Fact]
    public async Task BrokenJsonIsReportedAndNoServersAreInvented()
    {
        var path = Write("mcp.json", "{ \"mcpServers\": { ");

        var inspection = await Inspector().InspectAsync(path);

        inspection.Servers.Should().BeEmpty();
        inspection.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    [Fact]
    public async Task AFileWithNoMcpSectionIsNotAnError()
    {
        var path = Write("mcp.json", "{ \"other\": true }");

        var inspection = await Inspector().InspectAsync(path);

        inspection.Servers.Should().BeEmpty();
        inspection.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task DeclarationsAreAttributedToTheFileRatherThanGuessedAtProvider()
    {
        // Which agent wrote a file the user named is not knowable from its contents.
        var path = Write("mcp.json", "{ \"mcpServers\": { \"catalog\": { \"url\": \"https://a.example/mcp\" } } }");

        var inspection = await Inspector().InspectAsync(path);

        inspection.Servers.Should().ContainSingle()
            .Which.ProviderId.Should().Be(McpFileInspector.FileProviderId);
    }

    [Fact]
    public async Task RejectsAnEmptyPath()
    {
        var act = async () => await Inspector().InspectAsync(" ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static McpFileInspector Inspector()
    {
        var fileSystem = new FileSystem();

        return new McpFileInspector(
            new IMcpConfigurationReader[]
            {
                new JsonMcpConfigurationReader(fileSystem),
                new TomlMcpConfigurationReader(fileSystem),
            },
            new McpDeclarationInspector(),

            // No adapters: a prober with none cannot make a request, which is the property these tests need.
            new McpProber([]),
            fileSystem);
    }

    private string Write(string fileName, string content)
    {
        var path = Path.Combine(_root, fileName);
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
