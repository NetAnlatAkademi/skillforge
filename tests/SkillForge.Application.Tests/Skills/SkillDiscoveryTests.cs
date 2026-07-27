using SkillForge.Application.Skills;
using SkillForge.Application.Tests.Fakes;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillDiscoveryTests
{
    [Fact]
    public void FindsEverySkillDirectlyUnderTheRoot()
    {
        var discovery = Create(
            "/skills/alpha/SKILL.md",
            "/skills/beta/SKILL.md",
            "/skills/gamma/SKILL.md");

        discovery.FindSkillDirectories("/skills")
            .Should().Equal("/skills/alpha", "/skills/beta", "/skills/gamma");
    }

    [Fact]
    public void FindsSkillsNestedSeveralLevelsDeep()
    {
        var discovery = Create("/repo/packages/team-a/reviewer/SKILL.md");

        discovery.FindSkillDirectories("/repo")
            .Should().Equal("/repo/packages/team-a/reviewer");
    }

    [Fact]
    public void OrderIsStableSoARunOverUnchangedInputReportsTheSameSequence()
    {
        var discovery = Create(
            "/skills/zulu/SKILL.md",
            "/skills/alpha/SKILL.md",
            "/skills/mike/SKILL.md");

        discovery.FindSkillDirectories("/skills")
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARootThatIsItselfASkillIsReturnedAsTheOnlySkill()
    {
        var discovery = Create("/skills/demo/SKILL.md", "/skills/demo/examples/nested/SKILL.md");

        // Nesting is not something the format supports, so a SKILL.md inside a skill is treated as material
        // the outer skill ships rather than a second skill.
        discovery.FindSkillDirectories("/skills/demo").Should().Equal("/skills/demo");
    }

    [Fact]
    public void ASkillNestedInsideAnotherSkillIsIgnored()
    {
        var discovery = Create(
            "/skills/outer/SKILL.md",
            "/skills/outer/fixtures/inner/SKILL.md",
            "/skills/other/SKILL.md");

        discovery.FindSkillDirectories("/skills").Should().Equal("/skills/other", "/skills/outer");
    }

    [Theory]
    [InlineData("/skills/node_modules/vendored/SKILL.md")]
    [InlineData("/skills/bin/copied/SKILL.md")]
    [InlineData("/skills/obj/copied/SKILL.md")]
    [InlineData("/skills/.git/worktree/SKILL.md")]
    [InlineData("/skills/artifacts/unpacked/SKILL.md")]
    public void ToolingDirectoriesAreSkipped(string vendoredSkill)
    {
        var discovery = Create("/skills/real/SKILL.md", vendoredSkill);

        discovery.FindSkillDirectories("/skills").Should().Equal("/skills/real");
    }

    [Fact]
    public void ADirectoryWithNoSkillsFindsNothing()
    {
        var discovery = Create("/skills/readme/NOTES.md");

        discovery.FindSkillDirectories("/skills").Should().BeEmpty();
    }

    [Fact]
    public void AMissingRootFindsNothingRatherThanThrowing()
    {
        var discovery = new SkillDiscovery(new FakeFileSystem());

        discovery.FindSkillDirectories("/nowhere").Should().BeEmpty();
    }

    [Fact]
    public void TheSkillFileNameIsMatchedCaseInsensitively()
    {
        // Windows and macOS would find 'skill.md' anyway; matching it everywhere keeps the result the same on
        // Linux rather than depending on the platform the scan runs on.
        var discovery = Create("/skills/demo/skill.md");

        discovery.FindSkillDirectories("/skills").Should().Equal("/skills/demo");
    }

    [Fact]
    public void RejectsABlankRoot()
    {
        var discovery = new SkillDiscovery(new FakeFileSystem());

        var act = () => discovery.FindSkillDirectories("  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RejectsAMissingFileSystem()
    {
        var act = () => new SkillDiscovery(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static SkillDiscovery Create(params string[] files)
    {
        var fileSystem = new FakeFileSystem();
        foreach (var file in files)
        {
            fileSystem.AddFile(file, "content");
        }

        return new SkillDiscovery(fileSystem);
    }
}
