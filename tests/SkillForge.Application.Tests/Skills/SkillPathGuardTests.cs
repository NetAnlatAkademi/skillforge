using SkillForge.Application.Skills;
using SkillForge.Application.Tests.Fakes;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillPathGuardTests
{
    private const string SkillDirectory = "/skills/demo";

    [Theory]
    [InlineData("/skills/demo/SKILL.md")]
    [InlineData("/skills/demo/references/notes.md")]
    [InlineData("references/notes.md")]
    [InlineData("./references/notes.md")]
    [InlineData("nested/../references/notes.md")]
    public void AcceptsPathsInsideTheSkill(string candidate)
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.IsInsideSkillDirectory(SkillDirectory, candidate).Should().BeTrue();
    }

    [Theory]
    [InlineData("/skills/other/SKILL.md")]
    [InlineData("../other/SKILL.md")]
    [InlineData("../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public void RejectsPathsOutsideTheSkill(string candidate)
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.IsInsideSkillDirectory(SkillDirectory, candidate).Should().BeFalse();
    }

    [Fact]
    public void RejectsASiblingDirectoryWithTheSamePrefix()
    {
        // '/skills/demo-backup' must not count as being inside '/skills/demo'.
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.IsInsideSkillDirectory(SkillDirectory, "/skills/demo-backup/SKILL.md").Should().BeFalse();
    }

    [Fact]
    public void TheSkillDirectoryItselfIsInside()
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.IsInsideSkillDirectory(SkillDirectory, SkillDirectory).Should().BeTrue();
    }

    [Fact]
    public void RejectsALinkWhoseTargetEscapes()
    {
        var fileSystem = new FakeFileSystem()
            .AddLink("/skills/demo/leak.env", "/elsewhere/secrets.env");
        var guard = new SkillPathGuard(fileSystem);

        guard.IsInsideSkillDirectory(SkillDirectory, "/skills/demo/leak.env").Should().BeFalse();
    }

    [Fact]
    public void AcceptsALinkWhoseTargetStaysInside()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile("/skills/demo/references/notes.md", "notes")
            .AddLink("/skills/demo/alias.md", "/skills/demo/references/notes.md");
        var guard = new SkillPathGuard(fileSystem);

        guard.IsInsideSkillDirectory(SkillDirectory, "/skills/demo/alias.md").Should().BeTrue();
    }

    [Fact]
    public void RelativePathsUseForwardSlashes()
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.ToRelativePath(SkillDirectory, "/skills/demo/references/notes.md")
            .Should().Be("references/notes.md");
    }

    [Fact]
    public void RelativePathOfTheSkillFileIsJustItsName()
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        guard.ToRelativePath(SkillDirectory, "/skills/demo/SKILL.md").Should().Be("SKILL.md");
    }

    [Fact]
    public void RejectsBlankArguments()
    {
        var guard = new SkillPathGuard(new FakeFileSystem());

        var blankRoot = () => guard.IsInsideSkillDirectory(" ", "SKILL.md");
        var blankCandidate = () => guard.IsInsideSkillDirectory(SkillDirectory, " ");

        blankRoot.Should().Throw<ArgumentException>();
        blankCandidate.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RejectsAMissingFileSystem()
    {
        var act = () => new SkillPathGuard(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
