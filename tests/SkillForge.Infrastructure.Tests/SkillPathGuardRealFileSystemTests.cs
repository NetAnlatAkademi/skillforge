using SkillForge.Application.Skills;

namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// <see cref="SkillPathGuard"/> over the real file system.
/// </summary>
/// <remarks>
/// The unit tests exercise the guard's logic against a fake. These cover what only the real
/// implementation can get wrong — notably that validation rules ask about paths that do not exist yet,
/// which must be answered rather than thrown at.
/// </remarks>
public sealed class SkillPathGuardRealFileSystemTests
{
    private readonly SkillPathGuard _guard = new(new FileSystem());
    private readonly string _skillDirectory = RepositoryPaths.Sample("valid-skill");

    [Fact]
    public void AnswersForAReferencedFileThatDoesNotExist()
    {
        // The SF0007 rule asks this question about every reference it finds, including broken ones.
        _guard.IsInsideSkillDirectory(_skillDirectory, "references/missing.md").Should().BeTrue();
    }

    [Fact]
    public void AnswersForAMissingPathOutsideTheSkill()
    {
        _guard.IsInsideSkillDirectory(_skillDirectory, "../missing-elsewhere.md").Should().BeFalse();
    }

    [Fact]
    public void AcceptsAnExistingFileInsideTheSkill()
    {
        _guard.IsInsideSkillDirectory(_skillDirectory, "references/notes.md").Should().BeTrue();
    }

    [Fact]
    public void RejectsAnExistingFileInAnotherSkill()
    {
        var otherSkill = Path.Combine(RepositoryPaths.Sample("dotnet-api-review"), "SKILL.md");

        _guard.IsInsideSkillDirectory(_skillDirectory, otherSkill).Should().BeFalse();
    }

    [Fact]
    public void RelativePathsUseForwardSlashesOnEveryPlatform()
    {
        var nested = Path.Combine(_skillDirectory, "references", "notes.md");

        _guard.ToRelativePath(_skillDirectory, nested).Should().Be("references/notes.md");
    }
}
