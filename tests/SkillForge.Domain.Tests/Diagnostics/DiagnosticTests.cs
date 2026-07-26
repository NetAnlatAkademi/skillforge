using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Tests.Diagnostics;

public sealed class DiagnosticTests
{
    [Fact]
    public void ErrorFactorySetsErrorSeverity()
    {
        var diagnostic = Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "not found");

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Code.Should().Be("SF0001");
        diagnostic.Message.Should().Be("not found");
    }

    [Fact]
    public void WarningFactorySetsWarningSeverity()
    {
        Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license")
            .Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void InfoFactorySetsInfoSeverity()
    {
        Diagnostic.Info(DiagnosticCodes.ContainsScript, "has a script")
            .Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void OptionalLocationDefaultsToUnknown()
    {
        var diagnostic = Diagnostic.Error(DiagnosticCodes.NameMissing, "missing");

        diagnostic.FilePath.Should().BeNull();
        diagnostic.Line.Should().BeNull();
        diagnostic.Suggestion.Should().BeNull();
    }

    [Fact]
    public void LocationIsCarriedThrough()
    {
        var diagnostic = Diagnostic.Warning(
            DiagnosticCodes.SkillFileTooLong,
            "too long",
            "SKILL.md",
            line: 642,
            suggestion: "split it");

        diagnostic.FilePath.Should().Be("SKILL.md");
        diagnostic.Line.Should().Be(642);
        diagnostic.Suggestion.Should().Be("split it");
    }

    [Fact]
    public void SeverityOrderingLetsCallersRankFindings()
    {
        // Reports list errors first, so the enum must sort that way.
        ((int)DiagnosticSeverity.Error).Should().BeGreaterThan((int)DiagnosticSeverity.Warning);
        ((int)DiagnosticSeverity.Warning).Should().BeGreaterThan((int)DiagnosticSeverity.Info);
    }
}
