using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation;

public sealed class ValidationSummaryTests
{
    [Fact]
    public void CountsBySeverity()
    {
        var summary = Domain.Validation.ValidationSummary.FromDiagnostics(
        [
            Diagnostic.Error("SF0004", "e1"),
            Diagnostic.Error("SF0005", "e2"),
            Diagnostic.Warning("SF1009", "w1"),
            Diagnostic.Info("SF2001", "i1"),
        ]);

        summary.Errors.Should().Be(2);
        summary.Warnings.Should().Be(1);
        summary.Info.Should().Be(1);
        summary.Total.Should().Be(4);
        summary.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NoDiagnosticsMeansValid()
    {
        var summary = Domain.Validation.ValidationSummary.FromDiagnostics([]);

        summary.Total.Should().Be(0);
        summary.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WarningsAndInfoLeaveTheSkillValid()
    {
        var summary = Domain.Validation.ValidationSummary.FromDiagnostics(
            [Diagnostic.Warning("SF1009", "w"), Diagnostic.Info("SF2001", "i")]);

        summary.IsValid.Should().BeTrue();
    }
}
