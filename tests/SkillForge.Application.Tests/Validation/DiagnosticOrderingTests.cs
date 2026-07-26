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

public sealed class MarkdownLinkExtractorTests
{
    [Fact]
    public void FindsAnInlineLink()
    {
        var links = MarkdownLinkExtractor.Extract("See [notes](references/notes.md).", bodyStartLine: 1);

        links.Should().ContainSingle();
        links[0].Target.Should().Be("references/notes.md");
        links[0].Line.Should().Be(1);
    }

    [Fact]
    public void ReportsLinesRelativeToTheFile()
    {
        var links = MarkdownLinkExtractor.Extract("intro\n\n[notes](references/notes.md)", bodyStartLine: 10);

        links.Should().ContainSingle().Which.Line.Should().Be(12);
    }

    [Fact]
    public void FindsSeveralLinksOnOneLine()
    {
        var links = MarkdownLinkExtractor.Extract("[a](a.md) and [b](b.md)", bodyStartLine: 1);

        links.Select(link => link.Target).Should().Equal("a.md", "b.md");
    }

    [Fact]
    public void FindsImageReferences()
    {
        var links = MarkdownLinkExtractor.Extract("![diagram](assets/flow.png)", bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("assets/flow.png");
    }

    [Theory]
    [InlineData("[x](https://example.com/page)")]
    [InlineData("[x](http://example.com)")]
    [InlineData("[x](mailto:someone@example.com)")]
    [InlineData("[x](#anchor)")]
    [InlineData("[x](/absolute/path)")]
    public void IgnoresAnythingThatIsNotALocalRelativePath(string body)
    {
        MarkdownLinkExtractor.Extract(body, bodyStartLine: 1).Should().BeEmpty();
    }

    [Fact]
    public void StripsAnAnchorFromALocalPath()
    {
        var links = MarkdownLinkExtractor.Extract("[x](references/notes.md#section)", bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("references/notes.md");
    }

    [Fact]
    public void StripsATitleFromALocalPath()
    {
        var links = MarkdownLinkExtractor.Extract("""[x](references/notes.md "The notes")""", bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("references/notes.md");
    }

    [Fact]
    public void NormalisesBackslashesToForwardSlashes()
    {
        var links = MarkdownLinkExtractor.Extract(@"[x](references\notes.md)", bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("references/notes.md");
    }

    [Fact]
    public void DecodesEscapedSpaces()
    {
        var links = MarkdownLinkExtractor.Extract("[x](references/my%20notes.md)", bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("references/my notes.md");
    }

    [Fact]
    public void IgnoresLinksInsideFencedCodeBlocks()
    {
        const string body = """
            Example:

            ```markdown
            [x](references/example.md)
            ```

            [real](references/real.md)
            """;

        var links = MarkdownLinkExtractor.Extract(body, bodyStartLine: 1);

        links.Should().ContainSingle().Which.Target.Should().Be("references/real.md");
    }

    [Fact]
    public void IgnoresLinksInsideTildeFencedBlocks()
    {
        const string body = "~~~\n[x](references/example.md)\n~~~";

        MarkdownLinkExtractor.Extract(body, bodyStartLine: 1).Should().BeEmpty();
    }

    [Fact]
    public void KeepsLineNumbersCorrectAcrossACodeBlock()
    {
        const string body = "```\nfenced\n```\n[real](references/real.md)";

        MarkdownLinkExtractor.Extract(body, bodyStartLine: 1).Should().ContainSingle()
            .Which.Line.Should().Be(4);
    }

    [Fact]
    public void IgnoresAnEmptyTarget()
    {
        MarkdownLinkExtractor.Extract("[x]()", bodyStartLine: 1).Should().BeEmpty();
    }

    [Fact]
    public void AnEmptyBodyHasNoLinks()
    {
        MarkdownLinkExtractor.Extract(string.Empty, bodyStartLine: 1).Should().BeEmpty();
    }

    [Fact]
    public void RejectsAMissingBody()
    {
        var act = () => MarkdownLinkExtractor.Extract(null!, bodyStartLine: 1);

        act.Should().Throw<ArgumentNullException>();
    }
}
