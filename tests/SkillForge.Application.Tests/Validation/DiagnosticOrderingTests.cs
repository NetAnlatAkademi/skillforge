using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation;

public sealed class DiagnosticOrderingTests
{
    [Fact]
    public void ErrorsComeBeforeWarningsWhichComeBeforeInfo()
    {
        var sorted = DiagnosticOrdering.Sort(
        [
            Diagnostic.Info("SF2001", "info"),
            Diagnostic.Warning("SF1009", "warning"),
            Diagnostic.Error("SF0004", "error"),
        ]);

        sorted.Select(diagnostic => diagnostic.Severity).Should().Equal(
            DiagnosticSeverity.Error,
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Info);
    }

    [Fact]
    public void WithinASeverityCodesAreOrdered()
    {
        var sorted = DiagnosticOrdering.Sort(
        [
            Diagnostic.Error("SF0009", "later"),
            Diagnostic.Error("SF0004", "earlier"),
        ]);

        sorted.Select(diagnostic => diagnostic.Code).Should().Equal("SF0004", "SF0009");
    }

    [Fact]
    public void WithinACodeFilesAreOrdered()
    {
        var sorted = DiagnosticOrdering.Sort(
        [
            Diagnostic.Error("SF0007", "b", "references/b.md"),
            Diagnostic.Error("SF0007", "a", "references/a.md"),
        ]);

        sorted.Select(diagnostic => diagnostic.FilePath).Should().Equal("references/a.md", "references/b.md");
    }

    [Fact]
    public void WithinAFileLinesAreOrdered()
    {
        var sorted = DiagnosticOrdering.Sort(
        [
            Diagnostic.Error("SF0007", "later", "SKILL.md", 20),
            Diagnostic.Error("SF0007", "earlier", "SKILL.md", 3),
        ]);

        sorted.Select(diagnostic => diagnostic.Line).Should().Equal(3, 20);
    }

    [Fact]
    public void DiagnosticsWithoutALocationComeFirstWithinTheirCode()
    {
        var sorted = DiagnosticOrdering.Sort(
        [
            Diagnostic.Error("SF0007", "located", "SKILL.md", 3),
            Diagnostic.Error("SF0007", "unlocated"),
        ]);

        sorted[0].Message.Should().Be("unlocated");
    }

    [Fact]
    public void SortingIsStableForIdenticallyRankedDiagnostics()
    {
        // Two findings that rank the same must keep the order they were produced in, so repeated runs
        // over unchanged input produce byte-identical reports.
        var first = Diagnostic.Warning("SF1005", "first url", "SKILL.md", 4);
        var second = Diagnostic.Warning("SF1005", "second url", "SKILL.md", 4);

        DiagnosticOrdering.Sort([first, second]).Should().Equal(first, second);
        DiagnosticOrdering.Sort([second, first]).Should().Equal(second, first);
    }

    [Fact]
    public void SortingAnEmptySequenceIsEmpty()
    {
        DiagnosticOrdering.Sort([]).Should().BeEmpty();
    }

    [Fact]
    public void RejectsAMissingSequence()
    {
        var act = () => DiagnosticOrdering.Sort(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
