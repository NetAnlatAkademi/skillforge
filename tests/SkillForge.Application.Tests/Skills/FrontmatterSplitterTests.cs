using SkillForge.Application.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class FrontmatterSplitterTests
{
    [Fact]
    public void SplitsFrontmatterFromBody()
    {
        const string content = "---\nname: demo\n---\n\n# Demo\n\nBody text.\n";

        var split = FrontmatterSplitter.TrySplit(content);

        split.Should().NotBeNull();
        split!.Yaml.Should().Be("name: demo");
        split.Body.Should().Be("# Demo\n\nBody text.\n");
        split.StartLine.Should().Be(1);
        split.EndLine.Should().Be(3);
    }

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        const string content = "---\r\nname: demo\r\n---\r\n\r\n# Demo\r\n";

        var split = FrontmatterSplitter.TrySplit(content);

        split.Should().NotBeNull();
        split!.Yaml.Should().Be("name: demo");
        split.Body.Should().Be("# Demo\n");
    }

    [Fact]
    public void ToleratesAByteOrderMark()
    {
        var content = '﻿' + "---\nname: demo\n---\n# Demo\n";

        var split = FrontmatterSplitter.TrySplit(content);

        split.Should().NotBeNull();
        split!.Yaml.Should().Be("name: demo");
    }

    [Fact]
    public void AcceptsThreeDotsAsClosingDelimiter()
    {
        var split = FrontmatterSplitter.TrySplit("---\nname: demo\n...\n# Demo\n");

        split.Should().NotBeNull();
        split!.EndLine.Should().Be(3);
    }

    [Fact]
    public void AllowsLeadingBlankLinesBeforeTheBlock()
    {
        var split = FrontmatterSplitter.TrySplit("\n\n---\nname: demo\n---\n");

        split.Should().NotBeNull();
        split!.StartLine.Should().Be(3);
    }

    [Fact]
    public void ReturnsNullWhenThereIsNoFrontmatter()
    {
        FrontmatterSplitter.TrySplit("# Demo\n\nJust a Markdown file.\n").Should().BeNull();
    }

    [Fact]
    public void ReturnsNullWhenTheBlockIsNeverClosed()
    {
        FrontmatterSplitter.TrySplit("---\nname: demo\n\n# Demo\n").Should().BeNull();
    }

    [Fact]
    public void ReturnsNullWhenContentPrecedesTheBlock()
    {
        // A '---' after prose is a horizontal rule, not frontmatter.
        FrontmatterSplitter.TrySplit("# Demo\n\n---\nname: demo\n---\n").Should().BeNull();
    }

    [Fact]
    public void EmptyBlockYieldsEmptyYaml()
    {
        var split = FrontmatterSplitter.TrySplit("---\n---\n# Demo\n");

        split.Should().NotBeNull();
        split!.Yaml.Should().BeEmpty();
        split.Body.Should().Be("# Demo\n");
    }

    [Fact]
    public void BodyIsEmptyWhenTheFileEndsWithTheBlock()
    {
        var split = FrontmatterSplitter.TrySplit("---\nname: demo\n---");

        split.Should().NotBeNull();
        split!.Body.Should().BeEmpty();
    }

    [Fact]
    public void TotalLineCountCoversTheWholeFile()
    {
        var split = FrontmatterSplitter.TrySplit("---\nname: demo\n---\nline four\n");

        split.Should().NotBeNull();
        // Four lines of text plus the empty string after the trailing newline.
        split!.TotalLineCount.Should().Be(5);
    }

    [Fact]
    public void CountLinesMatchesTheSplitterView()
    {
        FrontmatterSplitter.CountLines("a\nb\r\nc").Should().Be(3);
    }

    [Fact]
    public void TrySplitRejectsNull()
    {
        var act = () => FrontmatterSplitter.TrySplit(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
