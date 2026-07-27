using System.Text.Json;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Reporting.Tests;

public sealed class JsonReportSerializerTests
{
    private readonly JsonReportSerializer _serializer = new();

    [Fact]
    public void ReportsTheFormatItHandles() => _serializer.Format.Should().Be("json");

    [Fact]
    public void MatchesTheDocumentedShape()
    {
        // The shape is a published contract; a CI script parses it.
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.", "SKILL.md", 1, "Add one."))));

        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        root.GetProperty("tool").GetProperty("name").GetString().Should().Be("SkillForge");
        root.GetProperty("tool").GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("skill").GetProperty("name").GetString().Should().Be("demo-skill");
        root.GetProperty("skill").GetProperty("version").GetString().Should().Be("1.2.3");
        root.GetProperty("summary").GetProperty("warnings").GetInt32().Should().Be(1);
        root.GetProperty("summary").GetProperty("valid").GetBoolean().Should().BeTrue();

        var diagnostic = root.GetProperty("diagnostics")[0];
        diagnostic.GetProperty("code").GetString().Should().Be("SF1009");
        diagnostic.GetProperty("severity").GetString().Should().Be("warning");
        diagnostic.GetProperty("line").GetInt32().Should().Be(1);
        diagnostic.GetProperty("suggestion").GetString().Should().Be("Add one.");
    }

    [Fact]
    public void SeverityNamesAreLowercase()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.NameMissing, "e"),
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "w"),
            Diagnostic.Info(DiagnosticCodes.ContainsScript, "i"))));

        document.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Select(item => item.GetProperty("severity").GetString())
            .Should().Equal("error", "warning", "info");
    }

    [Fact]
    public void MissingLocationsAreNullRatherThanAbsent()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.NameMissing, "No name."))));

        var diagnostic = document.RootElement.GetProperty("diagnostics")[0];
        diagnostic.GetProperty("filePath").ValueKind.Should().Be(JsonValueKind.Null);
        diagnostic.GetProperty("line").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void AnEmptyReportStillHasEveryTopLevelField()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report()));

        document.RootElement.GetProperty("diagnostics").GetArrayLength().Should().Be(0);
        document.RootElement.GetProperty("summary").GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void RejectsAMissingReport()
    {
        var act = () => _serializer.Serialize(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ValidationReport Report(params Diagnostic[] diagnostics) =>
        new("demo-skill", "/skills/demo", diagnostics, ValidationSummary.FromDiagnostics(diagnostics), "1.2.3");
}
