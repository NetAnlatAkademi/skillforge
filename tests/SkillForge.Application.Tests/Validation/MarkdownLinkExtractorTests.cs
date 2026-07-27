using SkillForge.Application.Validation;

namespace SkillForge.Application.Tests.Validation;

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
