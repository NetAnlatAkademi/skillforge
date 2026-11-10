using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp.Skills;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// The manifest rules, one sentence of the specification each, without any JSON or network.
/// </summary>
public sealed class McpSkillManifestRulesTests
{
    private const string SkillUri = "skill://a/SKILL.md";
    private const string Frontmatter = """{"name":"a","description":"b"}""";
    private static readonly string GoodDigest = "sha256:" + new string('a', 64);

    [Fact]
    public void AConformingStaticManifestHasNoProblems() =>
        McpSkillManifestRules.Problems(Static(new McpSkillResource(SkillUri, GoodDigest, 10))).Should().BeEmpty();

    [Fact]
    public void ADynamicManifestHasNoProblems() =>
        McpSkillManifestRules.Problems(new McpSkillEntry(SkillUri, Frontmatter, McpSkillManifest.Dynamic))
            .Should().BeEmpty("dynamic is a form the specification defines, not a fault");

    [Fact]
    public void AnInvalidManifestReportsWhyAndNothingElse() =>
        McpSkillManifestRules.Problems(new McpSkillEntry(SkillUri, Frontmatter, McpSkillManifest.Invalid("resources is missing")))
            .Should().Equal("resources is missing");

    [Fact]
    public void TheSkillFileMustBeInItsOwnManifest() =>
        McpSkillManifestRules.Problems(Static(new McpSkillResource("skill://a/other.md", GoodDigest, 1)))
            .Should().ContainSingle().Which.Should().Contain("does not name the skill's own SKILL.md");

    [Fact]
    public void AFileListedTwiceIsAProblem() =>
        McpSkillManifestRules.Problems(Static(
                new McpSkillResource(SkillUri, GoodDigest, 1),
                new McpSkillResource(SkillUri, GoodDigest, 1)))
            .Should().ContainSingle().Which.Should().Contain("more than once");

    [Theory]
    [InlineData(null, "declares no size")]
    [InlineData(-1L, "negative size")]
    public void ASizeThatCannotBeComparedIsAProblem(long? size, string expected) =>
        McpSkillManifestRules.Problems(Static(new McpSkillResource(SkillUri, GoodDigest, size)))
            .Should().ContainSingle().Which.Should().Contain(expected);

    [Fact]
    public void MoreFilesThanTheSpecificationAllowsIsAProblem()
    {
        var resources = Enumerable.Range(0, McpSkillManifestRules.MaxResources)
            .Select(index => new McpSkillResource($"skill://a/f{index}.md", GoodDigest, 1))
            .Append(new McpSkillResource(SkillUri, GoodDigest, 1))
            .ToArray();

        McpSkillManifestRules.Problems(Static(resources))
            .Should().ContainSingle().Which.Should().Contain("over the specification's limit of 512");
    }

    [Fact]
    public void SizesChosenToOverflowAreReportedNotThrown() =>
        McpSkillManifestRules.Problems(Static(
                new McpSkillResource(SkillUri, GoodDigest, long.MaxValue),
                new McpSkillResource("skill://a/b.md", GoodDigest, long.MaxValue)))
            .Should().ContainSingle().Which.Should().Contain("over the specification's limit");

    [Fact]
    public void AMissingFrontmatterIsAProblem() =>
        McpSkillManifestRules.Problems(new McpSkillEntry(SkillUri, null, McpSkillManifest.Static([new McpSkillResource(SkillUri, GoodDigest, 1)])))
            .Should().Equal("the entry carries no frontmatter object");

    private static McpSkillEntry Static(params McpSkillResource[] resources) =>
        new(SkillUri, Frontmatter, McpSkillManifest.Static(resources));
}
