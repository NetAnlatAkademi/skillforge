using SkillForge.Application.Skills;
using SkillForge.Application.Tests.Fakes;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillLoaderTests
{
    private const string SkillDirectory = "/skills/demo";
    private const string SkillFile = "/skills/demo/SKILL.md";

    private const string ValidSkillFile = """
        ---
        name: demo
        description: Use this skill when demonstrating the loader.
        ---

        # Demo

        Body text.
        """;

    [Fact]
    public async Task LoadsASkillFromItsDirectory()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning(name: "demo"));

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("demo");
        result.Value.DirectoryPath.Should().Be(SkillDirectory);
        result.Value.SkillFilePath.Should().Be(SkillFile);
    }

    [Fact]
    public async Task LoadsASkillFromTheSkillFilePath()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillFile, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DirectoryPath.Should().Be(SkillDirectory);
    }

    [Fact]
    public async Task ReportsSF0001WhenTheDirectoryHasNoSkillFile()
    {
        var fileSystem = new FakeFileSystem().AddDirectory(SkillDirectory);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task ReportsSF0001WhenThePathDoesNotExistAtAll()
    {
        var loader = new SkillLoader(new FakeFileSystem(), StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync("/nowhere", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    public async Task ReportsSF0001WhenTheSkillFileExistsButCannotBeRead(Type failureType)
    {
        // A locked or permission-denied SKILL.md must read as a diagnostic, not a stack trace.
        var failure = (Exception)Activator.CreateInstance(failureType, "denied")!;
        var fileSystem = new FakeFileSystem().FailReadWith(SkillFile, failure);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
        diagnostic.Message.Should().Contain("could not be read");
        diagnostic.FilePath.Should().Be(SkillDefinition.SkillFileName);
    }

    [Fact]
    public async Task AnEmptySkillFileReportsSF0002()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, string.Empty);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.FrontmatterNotFound);
    }

    [Fact]
    public async Task ReportsSF0002WhenThereIsNoFrontmatter()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, "# Demo\n\nNo frontmatter here.\n");
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.FrontmatterNotFound);
        diagnostic.FilePath.Should().Be(SkillDefinition.SkillFileName);
        diagnostic.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PropagatesParserFailureWithoutBuildingASkill()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Failing());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.FrontmatterNotParsable);
    }

    [Fact]
    public async Task CarriesParserWarningsOnASuccessfulLoad()
    {
        var duplicate = Diagnostic.Error(DiagnosticCodes.DuplicateMetadataField, "declared twice");
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(
            fileSystem,
            StubFrontmatterParser.Returning(diagnostics: [duplicate]));

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.DuplicateMetadataField);
    }

    [Fact]
    public async Task PassesTheYamlBlockAndItsStartLineToTheParser()
    {
        var parser = StubFrontmatterParser.Returning();
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(fileSystem, parser);

        await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        parser.ReceivedYaml.Should().Contain("name: demo");
        parser.ReceivedYaml.Should().NotContain("---");
        parser.ReceivedStartLine.Should().Be(1);
    }

    [Fact]
    public async Task NameAndDescriptionAreEmptyStringsWhenTheFrontmatterOmitsThem()
    {
        // Required-field rules own SF0004 and SF0005; the loader only models what is there.
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(
            fileSystem,
            StubFrontmatterParser.Returning(name: null, description: null));

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().BeEmpty();
        result.Value.Description.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task InventoriesFilesWithForwardSlashRelativePaths()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile("/skills/demo/references/notes.md", "notes")
            .AddFile("/skills/demo/scripts/analyze.ps1", "Write-Host 'hi'");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Value!.Resources.Select(resource => resource.RelativePath).Should().Equal(
            "SKILL.md",
            "references/notes.md",
            "scripts/analyze.ps1");
    }

    [Fact]
    public async Task ResourceOrderIsDeterministic()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile("/skills/demo/z.md", "z")
            .AddFile("/skills/demo/a.md", "a")
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile("/skills/demo/m.md", "m");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Value!.Resources.Select(resource => resource.RelativePath)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task ClassifiesAndSizesResources()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile("/skills/demo/scripts/analyze.ps1", "12345");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        var script = result.Value!.Resources.Single(resource => resource.RelativePath.EndsWith(".ps1", StringComparison.Ordinal));
        script.Kind.Should().Be(SkillResourceKind.Script);
        script.SizeInBytes.Should().Be(5);
    }

    [Theory]
    [InlineData("/skills/demo/bin/tool.dll")]
    [InlineData("/skills/demo/obj/temp.txt")]
    [InlineData("/skills/demo/.git/config")]
    [InlineData("/skills/demo/node_modules/pkg/index.js")]
    public async Task SkipsToolingDirectories(string ignoredFile)
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile(ignoredFile, "content");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Value!.Resources.Should().ContainSingle()
            .Which.RelativePath.Should().Be("SKILL.md");
    }

    [Fact]
    public async Task ReportsSF0008AndSkipsALinkPointingOutsideTheSkill()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile("/elsewhere/secrets.env", "TOKEN=abc")
            .AddLink("/skills/demo/leak.env", "/elsewhere/secrets.env");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Resources.Should().ContainSingle()
            .Which.RelativePath.Should().Be("SKILL.md");
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PathEscapesSkillDirectory);
    }

    [Fact]
    public async Task AcceptsALinkThatStaysInsideTheSkill()
    {
        var fileSystem = new FakeFileSystem()
            .AddFile(SkillFile, ValidSkillFile)
            .AddFile("/skills/demo/references/notes.md", "notes")
            .AddLink("/skills/demo/alias.md", "/skills/demo/references/notes.md");

        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Diagnostics.Should().BeEmpty();
        result.Value!.Resources.Should().HaveCount(3);
    }

    [Fact]
    public async Task RecordsTheBodyAndLineCount()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, ValidSkillFile);
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Value!.Body.Should().StartWith("# Demo");
        result.Value.SkillFileLineCount.Should().Be(8);
    }

    [Fact]
    public async Task FrontmatterLineNumbersComeFromTheFileNotTheParser()
    {
        var fileSystem = new FakeFileSystem().AddFile(SkillFile, "\n---\nname: demo\n---\n# Demo\n");
        var loader = new SkillLoader(fileSystem, StubFrontmatterParser.Returning());

        var result = await loader.LoadAsync(SkillDirectory, CancellationToken.None);

        result.Value!.Frontmatter.StartLine.Should().Be(2);
        result.Value.Frontmatter.EndLine.Should().Be(4);
    }

    [Fact]
    public async Task RejectsABlankPath()
    {
        var loader = new SkillLoader(new FakeFileSystem(), StubFrontmatterParser.Returning());

        var act = async () => await loader.LoadAsync("   ", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void RejectsMissingDependencies()
    {
        var withoutFileSystem = () => new SkillLoader(null!, StubFrontmatterParser.Returning());
        var withoutParser = () => new SkillLoader(new FakeFileSystem(), null!);

        withoutFileSystem.Should().Throw<ArgumentNullException>();
        withoutParser.Should().Throw<ArgumentNullException>();
    }
}
