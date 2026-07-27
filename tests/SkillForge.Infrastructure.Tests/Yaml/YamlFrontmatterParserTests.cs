using SkillForge.Domain.Diagnostics;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests.Yaml;

public sealed class YamlFrontmatterParserTests
{
    private const string FilePath = "SKILL.md";

    private readonly YamlFrontmatterParser _parser = new();

    [Fact]
    public void ReadsEveryKnownField()
    {
        const string yaml = """
            name: dotnet-api-review
            description: Reviews ASP.NET Core APIs.
            license: MIT
            compatibility:
              - codex
              - claude-code
            metadata:
              author: skillforge
              version: 1.0.0
            allowed-tools:
              - filesystem.read
            """;

        var result = _parser.Parse(yaml, startLine: 1, FilePath);

        result.IsSuccess.Should().BeTrue();
        var frontmatter = result.Value!;
        frontmatter.Name.Should().Be("dotnet-api-review");
        frontmatter.Description.Should().Be("Reviews ASP.NET Core APIs.");
        frontmatter.License.Should().Be("MIT");
        frontmatter.Compatibility.Should().Equal("codex", "claude-code");
        frontmatter.AllowedTools.Should().Equal("filesystem.read");
        frontmatter.Version.Should().Be("1.0.0");
        frontmatter.Author.Should().Be("skillforge");
    }

    [Fact]
    public void MissingFieldsBecomeNullRatherThanErrors()
    {
        var result = _parser.Parse("name: demo", startLine: 1, FilePath);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().BeNull();
        result.Value.License.Should().BeNull();
        result.Value.Compatibility.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnEmptyBlockParsesToEmptyFrontmatter()
    {
        var result = _parser.Parse(string.Empty, startLine: 1, FilePath);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().BeNull();
    }

    [Fact]
    public void AcceptsASingleValueWhereAListIsExpected()
    {
        var result = _parser.Parse("compatibility: codex", startLine: 1, FilePath);

        result.Value!.Compatibility.Should().Equal("codex");
    }

    [Fact]
    public void TrimsSurroundingWhitespace()
    {
        var result = _parser.Parse("name:    demo   ", startLine: 1, FilePath);

        result.Value!.Name.Should().Be("demo");
    }

    [Fact]
    public void BlankValuesAreTreatedAsAbsent()
    {
        var result = _parser.Parse("name:\ndescription: \"\"", startLine: 1, FilePath);

        result.Value!.Name.Should().BeNull();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void ReportsSF0003ForUnparsableYamlWithoutThrowing()
    {
        // Misindented sequence item: the most common hand-editing mistake.
        const string yaml = "name: demo\ncompatibility:\n  - codex\n - claude-code";

        var result = _parser.Parse(yaml, startLine: 1, FilePath);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.FrontmatterNotParsable);
        diagnostic.FilePath.Should().Be(FilePath);
        diagnostic.Line.Should().BeGreaterThan(1);
        diagnostic.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReportsSF0003WhenTheBlockIsNotKeyValuePairs()
    {
        var result = _parser.Parse("- just\n- a\n- list", startLine: 1, FilePath);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.FrontmatterNotParsable);
    }

    [Fact]
    public void ReportsSF0009ForADuplicatedField()
    {
        const string yaml = "name: first\ndescription: A description.\nname: second";

        var result = _parser.Parse(yaml, startLine: 1, FilePath);

        result.IsSuccess.Should().BeFalse();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.DuplicateMetadataField);
        diagnostic.Line.Should().Be(4); // startLine 1 + third yaml line
        diagnostic.Message.Should().Contain("'name'");
        diagnostic.Message.Should().Contain("line 2"); // where it was first declared
    }

    [Fact]
    public void SF0009SupersedesSF0003ForTheSameMistake()
    {
        // A duplicate key also makes the YAML parser fail. Reporting both would describe one mistake
        // twice, so only the precise diagnostic is kept.
        var result = _parser.Parse("name: first\nname: second", startLine: 1, FilePath);

        result.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.FrontmatterNotParsable);
    }

    [Fact]
    public void ReportsEveryRepeatOfADuplicatedField()
    {
        var result = _parser.Parse("name: a\nname: b\nname: c", startLine: 1, FilePath);

        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Select(diagnostic => diagnostic.Line).Should().Equal(3, 4);
    }

    [Fact]
    public void DiagnosticLinesAreOffsetByTheFrontmatterStartLine()
    {
        const string yaml = "name: first\nname: second";

        var result = _parser.Parse(yaml, startLine: 10, FilePath);

        result.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(12);
        result.Diagnostics[0].FilePath.Should().Be(FilePath);
    }

    [Fact]
    public void CommentsAndListItemsAreNotMistakenForFields()
    {
        const string yaml = """
            # name: commented out
            compatibility:
              - codex
            name: demo
            """;

        var result = _parser.Parse(yaml, startLine: 1, FilePath);

        result.Diagnostics.Should().BeEmpty();
        result.Value!.Name.Should().Be("demo");
    }

    [Fact]
    public void NonScalarMetadataEntriesAreIgnored()
    {
        const string yaml = """
            name: demo
            metadata:
              author: skillforge
              nested:
                - not-a-scalar
            """;

        var result = _parser.Parse(yaml, startLine: 1, FilePath);

        result.Value!.Metadata.Should().ContainKey("author");
        result.Value.Metadata.Should().NotContainKey("nested");
    }

    [Fact]
    public void ATopLevelVersionIsReadAndReported()
    {
        // The trap: every other field a skill declares is top-level, so authors write version there too. It used to
        // be discarded in silence -- SF0010 never checked it, inspect/pack/diff showed nothing, SF6001 could not
        // fire. Read it, and say where it belongs.
        var result = _parser.Parse(
            """
            name: demo
            version: 2.1.0
            """,
            startLine: 1,
            FilePath);

        result.Value!.Version.Should().Be("2.1.0");

        var diagnostic = result.Diagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.VersionOutsideMetadata).Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Fix.Should().Contain("metadata:").And.Contain("version: 2.1.0");
    }

    [Fact]
    public void MetadataVersionWinsWhenBothArePresent()
    {
        // The author said the same thing twice; believe the spelling the schema defines.
        var result = _parser.Parse(
            """
            name: demo
            version: 9.9.9
            metadata:
              version: 2.1.0
            """,
            startLine: 1,
            FilePath);

        result.Value!.Version.Should().Be("2.1.0");
        result.Diagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.VersionOutsideMetadata)
            .Which.Message.Should().Contain("as well as");
    }

    [Fact]
    public void AVersionInTheRightPlaceIsNotReported()
    {
        var result = _parser.Parse(
            """
            name: demo
            metadata:
              version: 2.1.0
            """,
            startLine: 1,
            FilePath);

        result.Value!.Version.Should().Be("2.1.0");
        result.Diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.VersionOutsideMetadata);
    }

    [Fact]
    public void RejectsInvalidArguments()
    {
        var nullYaml = () => _parser.Parse(null!, 1, FilePath);
        var blankPath = () => _parser.Parse("name: demo", 1, " ");

        nullYaml.Should().Throw<ArgumentNullException>();
        blankPath.Should().Throw<ArgumentException>();
    }
}
