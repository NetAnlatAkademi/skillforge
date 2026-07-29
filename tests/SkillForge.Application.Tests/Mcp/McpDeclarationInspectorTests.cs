using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// The static checks read a declaration and nothing else. Most of these tests are about what they refuse to report,
/// because on real input the band's only firing check already speaks about three servers out of four.
/// </summary>
public sealed class McpDeclarationInspectorTests
{
    private readonly McpDeclarationInspector _inspector = new();

    [Fact]
    public void EverythingInTheBandIsInformational()
    {
        // migrate inspect describes and exits zero; a warning that fires on three quarters of real declarations would
        // be the SF1009 shape. Pinned as a test because it is a decision, not an accident.
        var findings = _inspector.Inspect(Stdio("unpinned", "npx", "-y", "some-mcp"));

        findings.Should().AllSatisfy(finding => finding.Severity.Should().Be(DiagnosticSeverity.Info));
    }

    [Theory]
    [InlineData("some-mcp")]
    [InlineData("@scope/some-mcp")]
    [InlineData("some-mcp@latest")]
    [InlineData("@scope/some-mcp@next")]
    public void ReportsAPackageThatIsNotPinnedToAVersion(string package)
    {
        // 'latest' and 'next' are the same moving target spelled out, and a scoped name's leading '@' is not a version
        // separator — the check has to know both.
        var findings = _inspector.Inspect(Stdio("server", "npx", "-y", package));

        findings.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpServerCommandNotPinned);
    }

    [Theory]
    [InlineData("some-mcp@1.4.2")]
    [InlineData("@scope/some-mcp@1.4.2")]
    [InlineData("@scope/some-mcp@0.1.0-beta.3")]
    public void SaysNothingAboutAPinnedPackage(string package)
    {
        _inspector.Inspect(Stdio("server", "npx", "-y", package)).Should().BeEmpty();
    }

    [Fact]
    public void SaysNothingAboutALocalExecutable()
    {
        // A file on disk does not change underneath you, which is the opposite of what this check is about. Codex's own
        // MCP server is declared exactly like this.
        var server = Stdio("node-repl", @"C:\Users\someone\bin\node_repl.exe");

        _inspector.Inspect(server).Should().BeEmpty();
    }

    [Fact]
    public void ReportsTheDeprecatedSseTransport()
    {
        var findings = _inspector.Inspect(Http("legacy", "https://example.test/sse"));

        findings.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpDeprecatedTransport);
    }

    [Fact]
    public void ReportsAPlaintextRemoteEndpoint()
    {
        var findings = _inspector.Inspect(Http("remote", "http://mcp.example.test/mcp"));

        var finding = findings.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.McpPlaintextEndpoint);
        finding.FilePath.Should().Be("/home/dev/.claude.json");
    }

    [Theory]
    [InlineData("http://127.0.0.1:8801/mcp")]
    [InlineData("http://localhost:3000/mcp")]
    [InlineData("https://mcp.example.test/mcp")]
    public void SaysNothingAboutLoopbackOrHttps(string url)
    {
        // Reporting a loopback address would train people to ignore the code: there is no network to cross.
        _inspector.Inspect(Http("server", url)).Should().BeEmpty();
    }

    [Fact]
    public void RejectsAMissingDeclaration()
    {
        var act = () => _inspector.Inspect(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static McpServerDeclaration Stdio(string name, string command, params string[] arguments) =>
        new(name, "claude-code", McpTransport.Stdio, command, arguments, [], "/home/dev/.claude.json");

    private static McpServerDeclaration Http(string name, string url) =>
        new(name, "claude-code", McpTransport.Http, url, [], [], "/home/dev/.claude.json");
}
