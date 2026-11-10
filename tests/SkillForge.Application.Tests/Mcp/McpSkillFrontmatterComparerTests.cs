using SkillForge.Application.Mcp;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// Field-by-field comparison of two parsed frontmatters. Line numbers differ between a JSON object and a file and must
/// never count as drift.
/// </summary>
public sealed class McpSkillFrontmatterComparerTests
{
    [Fact]
    public void IdenticalFieldsOnDifferentLinesAgree()
    {
        var described = Frontmatter("a", "b") with { StartLine = 0, EndLine = 1 };
        var served = Frontmatter("a", "b") with { StartLine = 1, EndLine = 4 };

        McpSkillFrontmatterComparer.Differences(described, served).Should().BeEmpty();
    }

    [Fact]
    public void ReportsEachDifferingFieldInAFixedOrder()
    {
        var described = Frontmatter("a", "b") with { AllowedTools = ["Read"], Metadata = Map(("version", "1.0")) };
        var served = Frontmatter("z", "b") with { AllowedTools = ["Read", "Bash"], Metadata = Map(("version", "2.0")) };

        McpSkillFrontmatterComparer.Differences(described, served).Should().Equal(
            "name: 'a' · 'z'",
            "allowed-tools: [Read] · [Read, Bash]",
            "metadata.version: '1.0' · '2.0'");
    }

    [Fact]
    public void AFieldPresentOnOneSideOnlyIsADifference() =>
        McpSkillFrontmatterComparer.Differences(Frontmatter("a", "b") with { License = "MIT" }, Frontmatter("a", "b"))
            .Should().Equal("license: 'MIT' · (absent)");

    private static SkillFrontmatter Frontmatter(string name, string description) =>
        new(name, description, null, [], [], Map(), 1, 4);

    private static Dictionary<string, string> Map(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
}
