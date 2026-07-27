using System.Text.Json;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Reporting.Tests;

public sealed class SarifReportSerializerTests
{
    private readonly SarifReportSerializer _serializer = new();

    [Fact]
    public void ReportsTheFormatItHandles() => _serializer.Format.Should().Be("sarif");

    [Fact]
    public void ProducesSarif210WithADriver()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.NameMissing, "No name.", "SKILL.md", 2))));

        var root = document.RootElement;
        root.GetProperty("version").GetString().Should().Be("2.1.0");
        root.GetProperty("$schema").GetString().Should().Contain("sarif-schema-2.1.0");

        var driver = root.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver");
        driver.GetProperty("name").GetString().Should().Be("SkillForge");
        driver.GetProperty("rules").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void EachDistinctCodeBecomesOneRuleReferencedByIndex()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "a", "SKILL.md", 1),
            Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "b", "SKILL.md", 2),
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "c", "SKILL.md", 3))));

        var run = document.RootElement.GetProperty("runs")[0];
        run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength().Should().Be(2);
        run.GetProperty("results").GetArrayLength().Should().Be(3);

        var indexes = run.GetProperty("results").EnumerateArray()
            .Select(result => result.GetProperty("ruleIndex").GetInt32())
            .ToArray();
        indexes.Should().OnlyContain(index => index >= 0 && index < 2);
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Error, "error")]
    [InlineData(DiagnosticSeverity.Warning, "warning")]
    [InlineData(DiagnosticSeverity.Info, "note")]
    public void SeverityMapsToASarifLevel(DiagnosticSeverity severity, string expected)
    {
        var diagnostic = new Diagnostic("SF0004", severity, "message", "SKILL.md", 1);

        using var document = JsonDocument.Parse(_serializer.Serialize(Report(diagnostic)));

        document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("level").GetString().Should().Be(expected);
    }

    [Fact]
    public void AFindingWithoutALineIsAnchoredAtLineOneBecauseSarifRequiresOne()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Warning(DiagnosticCodes.SkillFileTooLong, "Too long.", "SKILL.md"))));

        document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region")
            .GetProperty("startLine").GetInt32().Should().Be(1);
    }

    [Fact]
    public void AFindingWithNoFileIsStillReportedWithoutALocation()
    {
        // Dropping it would hide a real problem from the pull request.
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.NameMissing, "No name."))));

        var result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        result.GetProperty("ruleId").GetString().Should().Be("SF0004");
        result.TryGetProperty("locations", out _).Should().BeFalse();
    }

    [Fact]
    public void TheMessageIncludesTheSuggestionSoAnAnnotationIsActionable()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.", "SKILL.md", 1, "Add one."))));

        document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("message").GetProperty("text").GetString()
            .Should().Be("No license. Add one.");
    }

    [Fact]
    public void UrisUseForwardSlashes()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report(
            Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "a", "references/notes.md", 1))));

        document.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation")
            .GetProperty("artifactLocation").GetProperty("uri").GetString()
            .Should().NotContain("\\");
    }

    [Fact]
    public void AnEmptyReportIsStillValidSarif()
    {
        using var document = JsonDocument.Parse(_serializer.Serialize(Report()));

        var run = document.RootElement.GetProperty("runs")[0];
        run.GetProperty("results").GetArrayLength().Should().Be(0);
        run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void RejectsAMissingReport()
    {
        var act = () => _serializer.Serialize(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ValidationReport Report(params Diagnostic[] diagnostics) =>
        new("demo-skill", "/skills/demo", diagnostics, ValidationSummary.FromDiagnostics(diagnostics));
}
