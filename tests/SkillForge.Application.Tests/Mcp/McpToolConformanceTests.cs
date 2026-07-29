using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// The conformance rules, tested without a server and without any JSON — which is the point of the split between reading
/// a payload and judging it.
/// </summary>
public sealed class McpToolConformanceTests
{
    [Fact]
    public void SaysNothingAboutAConformingTool()
    {
        var tool = Tool("get_weather", header: new McpHeaderAnnotation("region", "Region", "string"));

        Check(tool).Should().BeEmpty();
    }

    [Fact]
    public void NeverJudgesTheSchemaDialect()
    {
        // The rule that was NOT written. A summary said 2026-07-28 requires JSON Schema 2020-12; the specification says
        // inputSchema defaults to 2020-12 when $schema is absent and then shows an explicit draft-07 schema as a valid
        // example. Shipping that rule would have failed conforming servers.
        var tool = Tool("calculate_sum", dialect: "http://json-schema.org/draft-07/schema#");

        Check(tool).Should().BeEmpty();
    }

    [Fact]
    public void ReportsAToolWithNoInputSchemaObject()
    {
        var finding = Check(Tool("broken", hasSchema: false)).Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpToolInputSchemaInvalid);
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
        finding.FilePath.Should().Be("/home/dev/.claude.json");
    }

    [Theory]
    [InlineData("", "is empty")]
    [InlineData("bad header", "not a valid HTTP field name")]
    [InlineData("Region\r\nX", "carriage return")]
    public void ReportsAHeaderNameAClientWouldRejectTheToolOver(string headerName, string expected)
    {
        var tool = Tool("t", header: new McpHeaderAnnotation("region", headerName, "string"));

        var finding = Check(tool).Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.McpToolHeaderAnnotationInvalid);
        finding.Message.Should().Contain(expected);
    }

    [Theory]
    [InlineData("integer", 0)]
    [InlineData("string", 0)]
    [InlineData("boolean", 0)]
    [InlineData("number", 1)]
    [InlineData("array", 1)]
    [InlineData(null, 1)]
    public void OnlyPermitsHeaderAnnotationsOnPrimitiveTypes(string? type, int expectedFindings)
    {
        // 'number' is named in the specification as not permitted even though it is primitive, which is exactly the kind
        // of detail a summary loses.
        var tool = Tool("t", header: new McpHeaderAnnotation("value", "Value", type));

        Check(tool).Should().HaveCount(expectedFindings);
    }

    [Fact]
    public void ReportsTwoAnnotationsSharingAHeaderNameRegardlessOfCasing()
    {
        var tool = new McpToolSummary(
            "t",
            true,
            null,
            [
                new McpHeaderAnnotation("a", "Region", "string"),
                new McpHeaderAnnotation("b", "region", "string"),
            ]);

        Check(tool).Should().ContainSingle()
            .Which.Message.Should().Contain("repeats the header name");
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("weird!name")]
    [InlineData("")]
    public void ReportsANameOutsideTheGuidance(string name)
    {
        Check(Tool(name)).Should().Contain(finding =>
            finding.Code == DiagnosticCodes.McpToolNameOutsideGuidance);
    }

    [Fact]
    public void ReportsANameOverAHundredAndTwentyEightCharacters()
    {
        Check(Tool(new string('t', 129))).Should().ContainSingle()
            .Which.Message.Should().Contain("129");
    }

    [Theory]
    [InlineData("getUser")]
    [InlineData("DATA_EXPORT_v2")]
    [InlineData("admin.tools.list")]
    public void AcceptsTheNamesTheSpecificationGivesAsValid(string name)
    {
        Check(Tool(name)).Should().BeEmpty();
    }

    [Fact]
    public void ReportsTwoToolsWithTheSameName()
    {
        var findings = McpToolConformance.Check(
            "server",
            "/home/dev/.claude.json",
            [Tool("search"), Tool("search")]);

        findings.Should().ContainSingle()
            .Which.Message.Should().Contain("2 tools called 'search'");
    }

    [Fact]
    public void SaysNothingAboutNoToolsAtAll()
    {
        McpToolConformance.Check("server", "/home/dev/.claude.json", []).Should().BeEmpty();
    }

    private static McpToolSummary Tool(
        string name,
        bool hasSchema = true,
        string? dialect = null,
        McpHeaderAnnotation? header = null) =>
        new(name, hasSchema, dialect, header is null ? [] : [header]);

    private static IReadOnlyList<Diagnostic> Check(McpToolSummary tool) =>
        McpToolConformance.Check("server", "/home/dev/.claude.json", [tool]);
}
