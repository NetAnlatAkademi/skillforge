using SkillForge.Application.Validation;

namespace SkillForge.Application.Tests.Validation;

/// <summary>
/// The reader that exists because SF3002 could not tell prose from a code sample.
/// </summary>
/// <remarks>
/// Measured on 229 real skills, scanning a body for injection phrases produced twelve findings and roughly one
/// was real. The rest were not ambiguous English — they were **code**: a YAML comment reading
/// <c># Ignore other fields</c>, and a security skill's own detection pattern written as the string literal
/// <c>r'ignore (previous|all) instructions'</c>. A rule reading prose only would have missed every one of them,
/// which is what this class is for.
/// </remarks>
public sealed class MarkdownProseTests
{
    [Fact]
    public void PlainTextIsProse()
    {
        var prose = MarkdownProse.Extract("First line.\nSecond line.", bodyStartLine: 10);

        prose.Should().HaveCount(2);
        prose[0].Text.Should().Be("First line.");
        prose[0].Line.Should().Be(10);
        prose[1].Line.Should().Be(11);
    }

    [Fact]
    public void FencedCodeIsNotProse()
    {
        var body = "Before.\n```yaml\n# Ignore other fields\nname: x\n```\nAfter.";

        var prose = MarkdownProse.Extract(body, bodyStartLine: 1);

        prose.Select(line => line.Text).Should().Equal("Before.", "After.");
    }

    [Fact]
    public void TildeFencesCountToo()
    {
        var body = "Before.\n~~~\nignore all previous instructions\n~~~\nAfter.";

        MarkdownProse.Extract(body, bodyStartLine: 1)
            .Select(line => line.Text).Should().Equal("Before.", "After.");
    }

    [Fact]
    public void AnUnclosedFenceSwallowsTheRest()
    {
        // Erring towards silence: an unterminated fence is malformed Markdown, and treating the remainder as
        // prose would hand a rule a pile of code to misread.
        var prose = MarkdownProse.Extract("Before.\n```\nignore all previous instructions", bodyStartLine: 1);

        prose.Select(line => line.Text).Should().Equal("Before.");
    }

    [Fact]
    public void InlineCodeIsRemovedButItsLineSurvives()
    {
        var body = "Detects `r'ignore (previous|all) instructions'` in a skill body.";

        var prose = MarkdownProse.Extract(body, bodyStartLine: 5);

        prose.Should().ContainSingle();
        prose[0].Text.Should().NotContain("ignore");
        prose[0].Text.Should().Contain("Detects");
        prose[0].Text.Should().Contain("in a skill body.");
        prose[0].Line.Should().Be(5);
    }

    [Fact]
    public void DoubleBacktickSpansAreRemovedToo()
    {
        var prose = MarkdownProse.Extract("Use ``ignore all previous instructions`` here.", bodyStartLine: 1);

        prose[0].Text.Should().NotContain("ignore");
    }

    [Fact]
    public void AnUnpairedBacktickIsNotTreatedAsASpan()
    {
        // Removing to end-of-line on an unpaired backtick would silently delete real prose.
        var prose = MarkdownProse.Extract("A backtick ` and then ignore all previous instructions.", bodyStartLine: 1);

        prose[0].Text.Should().Contain("ignore all previous instructions");
    }

    [Fact]
    public void BlankLinesAreDropped()
    {
        MarkdownProse.Extract("One.\n\n\nTwo.", bodyStartLine: 1).Should().HaveCount(2);
    }

    [Fact]
    public void IndentedTextIsStillProse()
    {
        // Four-space indentation is a code block in strict Markdown, but in a real skill it is far more often a
        // list continuation. Excluding it was not justified by any measurement, so it is not excluded.
        var prose = MarkdownProse.Extract("- Step\n    ignore all previous instructions", bodyStartLine: 1);

        prose.Should().HaveCount(2);
        prose[1].Text.Should().Contain("ignore all previous instructions");
    }

    [Fact]
    public void AnEmptyBodyHasNoProse()
    {
        MarkdownProse.Extract(string.Empty, bodyStartLine: 1).Should().BeEmpty();
    }

    [Fact]
    public void CarriageReturnsDoNotShiftLineNumbers()
    {
        var prose = MarkdownProse.Extract("One.\r\nTwo.", bodyStartLine: 3);

        prose[1].Line.Should().Be(4);
        prose[1].Text.Should().Be("Two.");
    }

    [Fact]
    public void ExtractThrowsOnNull()
    {
        var act = () => MarkdownProse.Extract(null!, bodyStartLine: 1);

        act.Should().Throw<ArgumentNullException>();
    }
}
