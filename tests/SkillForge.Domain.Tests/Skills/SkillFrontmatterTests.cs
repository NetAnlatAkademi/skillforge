using SkillForge.Domain.Skills;

namespace SkillForge.Domain.Tests.Skills;

public sealed class SkillFrontmatterTests
{
    [Fact]
    public void EmptyHasNoFieldsSet()
    {
        var frontmatter = SkillFrontmatter.Empty(startLine: 1, endLine: 2);

        frontmatter.Name.Should().BeNull();
        frontmatter.Description.Should().BeNull();
        frontmatter.License.Should().BeNull();
        frontmatter.Compatibility.Should().BeEmpty();
        frontmatter.AllowedTools.Should().BeEmpty();
        frontmatter.Metadata.Should().BeEmpty();
        frontmatter.StartLine.Should().Be(1);
        frontmatter.EndLine.Should().Be(2);
    }

    [Fact]
    public void VersionAndAuthorAreReadFromMetadata()
    {
        var frontmatter = Create(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "1.2.3",
            ["author"] = "skillforge",
        });

        frontmatter.Version.Should().Be("1.2.3");
        frontmatter.Author.Should().Be("skillforge");
    }

    [Fact]
    public void VersionAndAuthorAreNullWhenMetadataIsAbsent()
    {
        var frontmatter = Create(new Dictionary<string, string>(StringComparer.Ordinal));

        frontmatter.Version.Should().BeNull();
        frontmatter.Author.Should().BeNull();
    }

    private static SkillFrontmatter Create(Dictionary<string, string> metadata) =>
        new("skill", "description", "MIT", [], [], metadata, 1, 5);
}
