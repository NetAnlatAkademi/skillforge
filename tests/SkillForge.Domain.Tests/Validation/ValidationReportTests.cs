using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Domain.Tests.Validation;

public sealed class ValidationReportTests
{
    [Fact]
    public void ForASkillNamesItAndSummarisesTheFindings()
    {
        var report = ValidationReport.For(
            CreateSkill(),
            [Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license")]);

        report.SkillName.Should().Be("demo");
        report.SkillPath.Should().Be("/skills/demo");
        report.Summary.Warnings.Should().Be(1);
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AnUnloadableSkillHasNoNameButKeepsItsPathAndFindings()
    {
        var report = ValidationReport.ForUnloadableSkill(
            "/skills/broken",
            [Diagnostic.Error(DiagnosticCodes.FrontmatterNotParsable, "bad yaml")]);

        report.SkillName.Should().BeEmpty();
        report.SkillPath.Should().Be("/skills/broken");
        report.Summary.Errors.Should().Be(1);
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void StrictModeOnlyChangesTheVerdictForWarnings()
    {
        var clean = ValidationReport.For(CreateSkill(), []);
        var warned = ValidationReport.For(
            CreateSkill(),
            [Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license")]);
        var failed = ValidationReport.For(
            CreateSkill(),
            [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        clean.HasFailed(strict: false).Should().BeFalse();
        clean.HasFailed(strict: true).Should().BeFalse();
        warned.HasFailed(strict: false).Should().BeFalse();
        warned.HasFailed(strict: true).Should().BeTrue();
        failed.HasFailed(strict: false).Should().BeTrue();
        failed.HasFailed(strict: true).Should().BeTrue();
    }

    [Fact]
    public void ForRejectsAMissingSkill()
    {
        var act = () => ValidationReport.For(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SummaryRejectsAMissingDiagnosticList()
    {
        var act = () => ValidationSummary.FromDiagnostics(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static SkillDefinition CreateSkill() =>
        new(
            "demo",
            "Use this skill when testing the report.",
            "/skills/demo",
            "/skills/demo/SKILL.md",
            SkillFrontmatter.Empty(1, 2),
            [],
            "# Demo",
            BodyStartLine: 4,
            SkillFileLineCount: 4);
}
