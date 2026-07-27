using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation;

public sealed class DiagnosticSuppressionTests
{
    [Fact]
    public void SuppressingNothingKeepsEverything()
    {
        var diagnostics = Findings();

        var result = DiagnosticSuppression.Apply(diagnostics, []);

        result.Kept.Should().BeSameAs(diagnostics, "no filtering means no copying");
        result.SuppressedCount.Should().Be(0);
    }

    [Fact]
    public void DropsTheCodesAskedFor()
    {
        var result = DiagnosticSuppression.Apply(Findings(), [DiagnosticCodes.LicenseMissing]);

        result.Kept.Select(diagnostic => diagnostic.Code)
            .Should().Equal(DiagnosticCodes.NameMissing, DiagnosticCodes.ContainsScript);
        result.SuppressedCount.Should().Be(1);
    }

    [Fact]
    public void CountsEveryOccurrenceNotEveryCode()
    {
        var diagnostics = new[]
        {
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "one"),
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "two"),
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "three"),
        };

        DiagnosticSuppression.Apply(diagnostics, [DiagnosticCodes.LicenseMissing])
            .SuppressedCount.Should().Be(3);
    }

    [Fact]
    public void CodesAreMatchedCaseInsensitively()
    {
        // Nobody should have to remember whether the flag wants sf1009 or SF1009.
        var result = DiagnosticSuppression.Apply(Findings(), ["sf1009"]);

        result.SuppressedCount.Should().Be(1);
    }

    [Fact]
    public void AnErrorCanBeSuppressedToo()
    {
        // Refusing would sound safer, but a repository that has decided a rule does not apply to it has a
        // reason we cannot see from here. The count is what keeps it honest.
        var result = DiagnosticSuppression.Apply(Findings(), [DiagnosticCodes.NameMissing]);

        result.Kept.Should().NotContain(diagnostic => diagnostic.Code == DiagnosticCodes.NameMissing);
        result.SuppressedCount.Should().Be(1);
    }

    [Fact]
    public void SuppressingACodeThatWasNotReportedChangesNothing()
    {
        var result = DiagnosticSuppression.Apply(Findings(), [DiagnosticCodes.SkillFileTooLong]);

        result.Kept.Should().HaveCount(3);
        result.SuppressedCount.Should().Be(0);
    }

    [Fact]
    public void OrderOfWhatSurvivesIsUnchanged()
    {
        var result = DiagnosticSuppression.Apply(Findings(), [DiagnosticCodes.LicenseMissing]);

        result.Kept.Select(diagnostic => diagnostic.Message).Should().Equal("no name", "has a script");
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var noDiagnostics = () => DiagnosticSuppression.Apply(null!, []);
        var noCodes = () => DiagnosticSuppression.Apply(Findings(), null!);

        noDiagnostics.Should().Throw<ArgumentNullException>();
        noCodes.Should().Throw<ArgumentNullException>();
    }

    private static Diagnostic[] Findings() =>
    [
        Diagnostic.Error(DiagnosticCodes.NameMissing, "no name"),
        Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license"),
        Diagnostic.Info(DiagnosticCodes.ContainsScript, "has a script"),
    ];
}
