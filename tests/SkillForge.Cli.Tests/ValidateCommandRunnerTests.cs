using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// The exit code is the CLI's real contract with CI, so each situation that produces one gets a test.
/// </summary>
public sealed class ValidateCommandRunnerTests
{
    [Fact]
    public async Task ACleanSkillExitsZero()
    {
        var runner = Build(out var renderer);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
        renderer.Rendered.Should().NotBeNull();
    }

    [Fact]
    public async Task AnErrorExitsOne()
    {
        var runner = Build(out _, validationFindings:
            [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        (await runner.RunAsync(Request(), CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task AWarningExitsZeroUnlessStrict()
    {
        var findings = new[] { Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license") };

        var lenient = await Build(out _, validationFindings: findings)
            .RunAsync(Request(), CancellationToken.None);
        var strict = await Build(out _, validationFindings: findings)
            .RunAsync(Request(strict: true), CancellationToken.None);

        lenient.Should().Be(0);
        strict.Should().Be(1);
    }

    [Fact]
    public async Task ASkillThatCannotBeLoadedExitsOneAndIsStillReported()
    {
        var runner = Build(out var renderer, loadFailure:
            Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync(Request("/nowhere"), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.SkillPath.Should().Be("/nowhere");
        renderer.Rendered.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task LoaderDiagnosticsAppearInTheReportAlongsideRuleFindings()
    {
        // A duplicated frontmatter field is something only the loader can see. Dropping it would leave the
        // user with an incomplete report.
        var runner = Build(
            out var renderer,
            loadDiagnostics: [Diagnostic.Error(DiagnosticCodes.DuplicateMetadataField, "declared twice")],
            validationFindings: [Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license")]);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().Equal(DiagnosticCodes.DuplicateMetadataField, DiagnosticCodes.LicenseMissing);
        renderer.Rendered.Summary.Errors.Should().Be(1);
        renderer.Rendered.Summary.Warnings.Should().Be(1);
    }

    [Theory]
    [InlineData(OutputFormat.Json, "\"schemaVersion\"")]
    [InlineData(OutputFormat.Sarif, "\"version\": \"2.1.0\"")]
    public async Task MachineReadableOutputGoesToTheRequestedFile(string format, string expected)
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, fileSystem: fileSystem, validationFindings:
            [Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license", "SKILL.md", 3)]);

        await runner.RunAsync(
            Request(format: format, outputPath: "/out/report.txt"),
            CancellationToken.None);

        fileSystem.ReadText("/out/report.txt").Should().Contain(expected);
    }

    [Fact]
    public async Task WritingToAFileStillShowsTheReportOnTheConsole()
    {
        // A CI log that says nothing about why a build failed is useless.
        var fileSystem = new FakeFileSystem();
        var runner = Build(out var renderer, fileSystem: fileSystem);

        await runner.RunAsync(
            Request(format: OutputFormat.Json, outputPath: "/out/report.json"),
            CancellationToken.None);

        renderer.Rendered.Should().NotBeNull();
    }

    [Fact]
    public async Task ADirectoryOfSkillsIsValidatedAsABatch()
    {
        var runner = Build(
            out var renderer,
            discovered: ["/skills/one", "/skills/two", "/skills/three"]);

        var exitCode = await runner.RunAsync(Request("/skills"), CancellationToken.None);

        exitCode.Should().Be(0);
        renderer.RenderedRun.Should().NotBeNull();
        renderer.RenderedRun!.SkillCount.Should().Be(3);
        renderer.Rendered.Should().BeNull("a batch is not also reported as a single skill");
    }

    [Fact]
    public async Task OneBadSkillFailsTheWholeBatch()
    {
        // A batch that passed because most of its skills were fine would be useless as a build gate.
        var runner = Build(
            out var renderer,
            discovered: ["/skills/one", "/skills/two"],
            validationFindings: [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        var exitCode = await runner.RunAsync(Request("/skills"), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.RenderedRun!.InvalidSkillCount.Should().Be(2);
    }

    [Fact]
    public async Task ABatchWarningOnlyFailsUnderStrict()
    {
        var findings = new[] { Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license") };

        var lenient = await Build(out _, discovered: ["/skills/one"], validationFindings: findings)
            .RunAsync(Request("/skills"), CancellationToken.None);
        var strict = await Build(out _, discovered: ["/skills/one"], validationFindings: findings)
            .RunAsync(Request("/skills", strict: true), CancellationToken.None);

        lenient.Should().Be(0);
        strict.Should().Be(1);
    }

    [Fact]
    public async Task ADirectoryThatIsItselfASkillIsNotTreatedAsABatch()
    {
        // Even when it contains other skills — a nested SKILL.md is far more likely to be a fixture the outer
        // skill ships than a second skill to validate.
        var fileSystem = new FakeFileSystem().WithFile("/skills/demo/SKILL.md", "---\nname: demo\n---\n");
        var runner = Build(out var renderer, fileSystem: fileSystem, discovered: ["/skills/demo/nested"]);

        await runner.RunAsync(Request("/skills/demo"), CancellationToken.None);

        renderer.Rendered.Should().NotBeNull();
        renderer.RenderedRun.Should().BeNull();
    }

    [Fact]
    public async Task ADirectoryWithNoSkillsFallsBackToTheSingleSkillErrorInsteadOfPretendingItPassed()
    {
        var runner = Build(
            out var renderer,
            discovered: [],
            loadFailure: Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync(Request("/empty"), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task BatchJsonNestsEverySkillUnderOneDocument()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, fileSystem: fileSystem, discovered: ["/skills/one", "/skills/two"]);

        await runner.RunAsync(
            Request("/skills", format: OutputFormat.Json, outputPath: "/out/report.json"),
            CancellationToken.None);

        var json = fileSystem.ReadText("/out/report.json");
        json.Should().Contain("\"skills\"");
        json.Should().Contain("\"root\"");
    }

    [Fact]
    public async Task BatchSarifIsOneRunSoASingleUploadCoversEverySkill()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(
            out _,
            fileSystem: fileSystem,
            discovered: ["/skills/one", "/skills/two"],
            validationFindings: [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name", "SKILL.md", 1)]);

        await runner.RunAsync(
            Request("/skills", format: OutputFormat.Sarif, outputPath: "/out/report.sarif"),
            CancellationToken.None);

        using var document = JsonDocument.Parse(fileSystem.ReadText("/out/report.sarif"));
        var runs = document.RootElement.GetProperty("runs");
        runs.GetArrayLength().Should().Be(1);
        runs[0].GetProperty("results").GetArrayLength().Should().Be(2, "one result per skill, merged");
    }

    [Fact]
    public void RejectsMissingDependencies()
    {
        var act = () => new ValidateCommandRunner(null!, null!, null!, null!, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ValidateRequest Request(
        string path = "/skills/demo",
        bool strict = false,
        string format = OutputFormat.Console,
        string? outputPath = null) =>
        new(path, strict, format, outputPath, new ReportRenderOptions());

    private static ValidateCommandRunner Build(
        out RecordingRenderer renderer,
        FakeFileSystem? fileSystem = null,
        Diagnostic? loadFailure = null,
        IReadOnlyList<Diagnostic>? loadDiagnostics = null,
        IReadOnlyList<Diagnostic>? validationFindings = null,
        IReadOnlyList<string>? discovered = null)
    {
        renderer = new RecordingRenderer();
        var files = fileSystem ?? new FakeFileSystem();

        var output = new ReportOutput(
            files,
            renderer,
            [new JsonReportSerializer(), new SarifReportSerializer()]);

        return new ValidateCommandRunner(
            new StubLoader(loadFailure, loadDiagnostics ?? []),
            new StubValidator(validationFindings ?? []),
            new StubDiscovery(discovered ?? []),
            files,
            output);
    }

    private sealed class StubDiscovery(IReadOnlyList<string> directories) : ISkillDiscovery
    {
        public IReadOnlyList<string> FindSkillDirectories(string rootDirectory) => directories;
    }

    private sealed class StubLoader(Diagnostic? failure, IReadOnlyList<Diagnostic> diagnostics) : ISkillLoader
    {
        public Task<OperationResult<SkillDefinition>> LoadAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(failure is null
                ? OperationResult<SkillDefinition>.Success(CreateSkill(), diagnostics)
                : OperationResult<SkillDefinition>.Failure(failure));

        private static SkillDefinition CreateSkill() =>
            new(
                "demo",
                "Use this skill when testing the CLI.",
                "/skills/demo",
                "/skills/demo/SKILL.md",
                SkillFrontmatter.Empty(1, 2),
                [],
                "# Demo",
                BodyStartLine: 4,
                SkillFileLineCount: 4);
    }

    private sealed class StubValidator(IReadOnlyList<Diagnostic> findings) : ISkillValidator
    {
        public Task<ValidationReport> ValidateAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken) =>
            Task.FromResult(ValidationReport.For(skill, findings));
    }
}
