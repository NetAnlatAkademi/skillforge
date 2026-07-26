using SkillForge.Application.Abstractions;
using SkillForge.Application.Skills;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// End-to-end loader tests over the committed sample skills, using the real file system and the real
/// YAML parser. These are the tests that would catch a wiring mistake between the two layers.
/// </summary>
public sealed class SkillLoaderIntegrationTests
{
    private readonly SkillLoader _loader = new(new FileSystem(), new YamlFrontmatterParser());

    [Fact]
    public async Task LoadsTheValidSample()
    {
        var result = await _loader.LoadAsync(RepositoryPaths.Sample("valid-skill"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();

        var skill = result.Value!;
        skill.Name.Should().Be("valid-skill");
        skill.Description.Should().NotBeNullOrWhiteSpace();
        skill.Frontmatter.License.Should().Be("MIT");
        skill.Frontmatter.Compatibility.Should().Contain("claude-code");
        skill.Frontmatter.Version.Should().Be("1.0.0");
        skill.Body.Should().StartWith("# Valid Skill");
    }

    [Fact]
    public async Task InventoriesTheValidSamplesFiles()
    {
        var result = await _loader.LoadAsync(RepositoryPaths.Sample("valid-skill"), CancellationToken.None);

        result.Value!.Resources.Select(resource => resource.RelativePath).Should().Equal(
            "SKILL.md",
            "references/notes.md");

        result.Value.Resources[0].Kind.Should().Be(SkillResourceKind.SkillDocument);
        result.Value.Resources[1].Kind.Should().Be(SkillResourceKind.Markdown);
        result.Value.Resources.Should().AllSatisfy(resource => resource.SizeInBytes.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ReportsSF0003ForTheInvalidFrontmatterSampleWithoutCrashing()
    {
        var result = await _loader.LoadAsync(
            RepositoryPaths.Sample("invalid-frontmatter"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.FrontmatterNotParsable);
    }

    [Fact]
    public async Task LoadsTheBrokenReferencesSampleBecauseItsFrontmatterIsFine()
    {
        // The missing files are a validation concern (SF0007), not a loading failure.
        var result = await _loader.LoadAsync(
            RepositoryPaths.Sample("broken-references"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("broken-references");
        result.Value.Resources.Select(resource => resource.RelativePath)
            .Should().Equal("SKILL.md", "references/notes.md");
    }

    [Fact]
    public async Task LoadsTheDocumentationSample()
    {
        var result = await _loader.LoadAsync(
            RepositoryPaths.Sample("dotnet-api-review"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();

        var skill = result.Value!;
        skill.Name.Should().Be("dotnet-api-review");
        skill.Frontmatter.Compatibility.Should().Equal("codex", "claude-code", "github-copilot");
        skill.Frontmatter.AllowedTools.Should().Equal("filesystem.read");
        skill.Resources.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcceptsThePathOfTheSkillFileItself()
    {
        var skillFile = Path.Combine(RepositoryPaths.Sample("valid-skill"), SkillDefinition.SkillFileName);

        var result = await _loader.LoadAsync(skillFile, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DirectoryPath.Should().Be(RepositoryPaths.Sample("valid-skill"));
    }

    [Fact]
    public async Task ReportsSF0001ForADirectoryWithoutASkillFile()
    {
        var result = await _loader.LoadAsync(RepositoryPaths.SamplesDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
        diagnostic.Suggestion.Should().Contain("skillforge init");
    }

    [Fact]
    public async Task ReportsSF0001ForAPathThatDoesNotExist()
    {
        var result = await _loader.LoadAsync(
            Path.Combine(RepositoryPaths.SamplesDirectory, "no-such-skill"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task RelativePathsAreResolvedAgainstTheWorkingDirectory()
    {
        var relativePath = Path.GetRelativePath(
            Directory.GetCurrentDirectory(),
            RepositoryPaths.Sample("valid-skill"));

        var result = await _loader.LoadAsync(relativePath, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Path.IsPathRooted(result.Value!.DirectoryPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadingIsCancellable()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await _loader.LoadAsync(
            RepositoryPaths.Sample("valid-skill"),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
