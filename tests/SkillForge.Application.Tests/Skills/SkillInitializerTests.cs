using SkillForge.Application.Abstractions;
using SkillForge.Application.Skills;
using SkillForge.Application.Tests.Fakes;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillInitializerTests
{
    private const string Target = "/work/my-skill";

    [Fact]
    public async Task CreatesTheSkillFileAndConfiguration()
    {
        var fileSystem = new FakeFileSystem();
        var result = await Initialize(fileSystem);

        result.IsSuccess.Should().BeTrue();
        fileSystem.FileExists($"{Target}/SKILL.md").Should().BeTrue();
        fileSystem.FileExists($"{Target}/skillforge.yaml").Should().BeTrue();
    }

    [Fact]
    public async Task CreatesTheConventionalDirectories()
    {
        var fileSystem = new FakeFileSystem();
        await Initialize(fileSystem);

        // Each gets a README because git does not track empty directories.
        foreach (var directory in SkillTemplate.Directories)
        {
            fileSystem.DirectoryExists($"{Target}/{directory}").Should().BeTrue(directory);
            fileSystem.FileExists($"{Target}/{directory}/README.md").Should().BeTrue(directory);
        }
    }

    [Fact]
    public async Task ReportsWhatItCreatedInAStableOrder()
    {
        var result = await Initialize(new FakeFileSystem());

        result.Value!.CreatedFiles.Should().BeInAscendingOrder(StringComparer.Ordinal);
        result.Value.DirectoryPath.Should().Be(Target);
    }

    [Fact]
    public async Task WritesTheNameDescriptionAuthorAndLicenseThatWereAskedFor()
    {
        var fileSystem = new FakeFileSystem();

        await Initialize(fileSystem, new SkillInitializationOptions(
            "my-skill",
            "Use this skill when reviewing something specific.",
            "Cagri",
            "Apache-2.0"));

        var content = fileSystem.ReadText($"{Target}/SKILL.md");
        content.Should().Contain("name: my-skill");
        content.Should().Contain("Use this skill when reviewing something specific.");
        content.Should().Contain("author: Cagri");
        content.Should().Contain("license: Apache-2.0");
    }

    [Fact]
    public async Task OmitsTheAuthorLineWhenNoAuthorIsGiven()
    {
        var fileSystem = new FakeFileSystem();
        await Initialize(fileSystem);

        fileSystem.ReadText($"{Target}/SKILL.md").Should().NotContain("author:");
    }

    [Theory]
    [InlineData("My-Skill")]
    [InlineData("my skill")]
    [InlineData("2skills")]
    [InlineData("x")]
    public async Task RefusesANameItWouldNotValidate(string name)
    {
        var fileSystem = new FakeFileSystem();

        var result = await Initialize(fileSystem, new SkillInitializationOptions(name));

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.NameInvalid);
        fileSystem.FileExists($"{Target}/SKILL.md").Should().BeFalse("nothing should be written");
    }

    [Fact]
    public async Task RejectsABlankTargetDirectory()
    {
        var initializer = new SkillInitializer(new FakeFileSystem());

        var act = async () => await initializer.InitializeAsync(
            "  ",
            new SkillInitializationOptions("my-skill"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static Task<Domain.OperationResult<SkillInitializationResult>> Initialize(
        FakeFileSystem fileSystem,
        SkillInitializationOptions? options = null) =>
        new SkillInitializer(fileSystem).InitializeAsync(
            Target,
            options ?? new SkillInitializationOptions("my-skill"),
            CancellationToken.None);
}
