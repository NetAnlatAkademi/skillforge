using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// The counting, the categories and — most of all — the difference between zero and unknown.
/// </summary>
/// <remarks>
/// A server that was never asked has no tool count. Reporting that as <c>0</c> would tell a reader the surface is
/// tiny when nobody looked at it, which is the one mistake this analysis must not make.
/// </remarks>
public sealed class McpSurfaceAnalyzerTests
{
    private static readonly McpSurfaceThresholds Thresholds = new(5, 10);

    [Fact]
    public void ASmallSurfaceIsInformationalAndSilent()
    {
        var report = Analyze(["read_file", "list_files"]);

        report.Servers.Should().ContainSingle().Which.Risk.Should().Be(SurfaceRisk.Informational);
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ASurfaceAtTheThresholdIsReportedAsSF7301()
    {
        var report = Analyze(["a_read", "b_read", "c_read", "d_read", "e_read"]);

        report.Diagnostics.Should().Contain(finding => finding.Code == "SF7301");
    }

    [Fact]
    public void ALargeSurfaceWithPrivilegedToolsIsHigh()
    {
        var report = Analyze(["a_read", "b_read", "c_read", "d_read", "delete_deployment"]);

        report.Servers[0].Risk.Should().Be(SurfaceRisk.High);
        report.Diagnostics.Should().Contain(finding => finding.Code == "SF7302");
    }

    [Fact]
    public void PrivilegedToolsAreNotReportedOnTheirOwnBelowTheThreshold()
    {
        // Otherwise every useful server produces a finding, and a finding everything produces is one nobody reads.
        var report = Analyze(["delete_deployment", "read_secret"]);

        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ToolsAreCategorisedByName()
    {
        var report = Analyze(["read_file", "delete_deployment", "read_secret", "grant_role"]);

        var surface = report.Servers[0];
        surface.WriteCapableTools.Should().Contain("delete_deployment");
        surface.CredentialCapableTools.Should().Contain("read_secret");
        surface.AdminTools.Should().Contain("grant_role");
        surface.PrivilegedTools.Should().HaveCount(3);
    }

    [Fact]
    public void AMetaToolThatHandsOutToolsCountsAsProgressiveDiscovery()
    {
        var report = Analyze(["search_tools", "a_read", "b_read", "c_read", "d_read"]);

        report.Servers[0].ProgressiveDiscoveryDetected.Should().BeTrue();
        report.Diagnostics.Should().NotContain(finding => finding.Code == "SF7401");
    }

    [Fact]
    public void ALargeSurfaceWithoutDiscoveryIsReportedAsSF7401()
    {
        var report = Analyze(["a_read", "b_read", "c_read", "d_read", "e_read"]);

        var finding = report.Diagnostics.Single(diagnostic => diagnostic.Code == "SF7401");
        finding.Severity.Should().Be(Domain.Diagnostics.DiagnosticSeverity.Info);
    }

    [Fact]
    public void AServerThatWasNotProbedHasNoCountsRatherThanZero()
    {
        var report = McpSurfaceAnalyzer.Analyze("/repo/.mcp.json", [Declaration()], [], Thresholds, []);

        var surface = report.Servers.Should().ContainSingle().Subject;
        surface.NotProbedReason.Should().NotBeNull();
        surface.Risk.Should().Be(SurfaceRisk.Informational);
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AStdioServerSaysWhyItWasNotAsked()
    {
        var stdio = new McpServerDeclaration(
            "local-tools",
            "file",
            McpTransport.Stdio,
            "npx",
            [],
            [],
            "/repo/.mcp.json");

        var report = McpSurfaceAnalyzer.Analyze("/repo/.mcp.json", [stdio], [], Thresholds, []);

        report.Servers[0].NotProbedReason.Should().Contain("never launches");
    }

    [Fact]
    public void ReadingDiagnosticsAreCarriedThrough()
    {
        var unreadable = Domain.Diagnostics.Diagnostic.Warning("SF1015", "could not be read", "/repo/.mcp.json");

        var report = McpSurfaceAnalyzer.Analyze("/repo/.mcp.json", [], [], Thresholds, [unreadable]);

        report.Diagnostics.Should().ContainSingle().Which.Code.Should().Be("SF1015");
    }

    private static McpSurfaceReport Analyze(IReadOnlyList<string> toolNames)
    {
        var probe = McpServerProbe.Answered(
            "enterprise-tools",
            ["2026-07-28"],
            ["tools"],
            "enterprise-tools",
            "1.0.0",
            "2026-07-28",
            [.. toolNames.Select(name => new McpToolSummary(name, true, null, []))]);

        return McpSurfaceAnalyzer.Analyze("/repo/.mcp.json", [Declaration()], [probe], Thresholds, []);
    }

    private static McpServerDeclaration Declaration() => new(
        "enterprise-tools",
        "file",
        McpTransport.Http,
        "https://tools.company.com/mcp",
        [],
        [],
        "/repo/.mcp.json");
}
