using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Domain.Tests.Skills;

/// <summary>
/// The model types are records on purpose: reports compare and copy them, so value semantics are part
/// of the contract rather than an implementation detail.
/// </summary>
public sealed class SkillModelTests
{
    [Fact]
    public void SkillFileNamesAreFixedConstants()
    {
        SkillDefinition.SkillFileName.Should().Be("SKILL.md");
        SkillDefinition.ConfigurationFileName.Should().Be("skillforge.yaml");
    }

    [Fact]
    public void SkillDefinitionCarriesEverythingTheLoaderFound()
    {
        var skill = CreateSkill();

        skill.Name.Should().Be("demo");
        skill.Description.Should().Be("Use this skill when testing the model.");
        skill.DirectoryPath.Should().Be("/skills/demo");
        skill.SkillFilePath.Should().Be("/skills/demo/SKILL.md");
        skill.Resources.Should().ContainSingle();
        skill.Body.Should().Be("# Demo");
        skill.SkillFileLineCount.Should().Be(7);
        skill.Frontmatter.Name.Should().Be("demo");
    }

    [Fact]
    public void SkillDefinitionsSharingTheirMembersAreEqual()
    {
        var frontmatter = CreateFrontmatter();
        IReadOnlyList<SkillResource> resources =
            [new SkillResource("SKILL.md", "/skills/demo/SKILL.md", SkillResourceKind.SkillDocument, 42)];

        var first = CreateSkill(frontmatter, resources);
        var second = CreateSkill(frontmatter, resources);

        first.Should().Be(second);
    }

    [Fact]
    public void EqualityDoesNotLookInsideCollectionMembers()
    {
        // Record equality compares Resources and Metadata by reference, so two independently built
        // definitions describing the same skill are not equal. Anything comparing skills — snapshot
        // tests, deduplication — has to compare the parts it cares about, not the whole record.
        CreateSkill().Should().NotBe(CreateSkill());
    }

    [Fact]
    public void CopyingASkillOverridesOnlyTheNamedMember()
    {
        var original = CreateSkill();

        var renamed = original with { Name = "renamed" };

        renamed.Name.Should().Be("renamed");
        renamed.Description.Should().Be(original.Description);
        renamed.Should().NotBe(original);
    }

    [Fact]
    public void SkillResourceRecordsWhatWasFoundOnDisk()
    {
        var resource = new SkillResource(
            "references/notes.md",
            "/skills/demo/references/notes.md",
            SkillResourceKind.Markdown,
            SizeInBytes: 128);

        resource.RelativePath.Should().Be("references/notes.md");
        resource.AbsolutePath.Should().Be("/skills/demo/references/notes.md");
        resource.Kind.Should().Be(SkillResourceKind.Markdown);
        resource.SizeInBytes.Should().Be(128);
    }

    [Fact]
    public void SkillResourcesWithTheSameContentAreEqual()
    {
        var first = new SkillResource("SKILL.md", "/skills/demo/SKILL.md", SkillResourceKind.SkillDocument, 10);
        var second = new SkillResource("SKILL.md", "/skills/demo/SKILL.md", SkillResourceKind.SkillDocument, 10);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void DiagnosticsWithTheSameContentAreEqual()
    {
        var first = new Diagnostic(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning, "no license");
        var second = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license");

        first.Should().Be(second);
    }

    [Fact]
    public void DiagnosticsDifferingInLocationAreNotEqual()
    {
        var atLineOne = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license", "SKILL.md", 1);
        var atLineTwo = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license", "SKILL.md", 2);

        atLineOne.Should().NotBe(atLineTwo);
    }

    private static SkillFrontmatter CreateFrontmatter() =>
        new(
            "demo",
            "Use this skill when testing the model.",
            "MIT",
            ["codex"],
            ["filesystem.read"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = "1.0.0" },
            1,
            5);

    private static SkillDefinition CreateSkill(
        SkillFrontmatter? frontmatter = null,
        IReadOnlyList<SkillResource>? resources = null) =>
        new(
            "demo",
            "Use this skill when testing the model.",
            "/skills/demo",
            "/skills/demo/SKILL.md",
            frontmatter ?? CreateFrontmatter(),
            resources ?? [new SkillResource("SKILL.md", "/skills/demo/SKILL.md", SkillResourceKind.SkillDocument, 42)],
            "# Demo",
            7);
}
