using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Tests;

/// <summary>
/// Inspect describes a skill's contents; it never fails a build over what it finds there, so the only exit
/// code that matters is whether the skill could be loaded at all.
/// </summary>
public sealed class InspectCommandRunnerTests
{
    [Fact]
    public async Task ALoadableSkillExitsZero()
    {
        var runner = Build(out _, out _);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task AnUnloadableSkillExitsOneAndTheFailureIsRendered()
    {
        var runner = Build(
            out var renderer,
            out _,
            loadFailure: Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task JsonFormatWritesJsonContainingTheSkillName()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, out _, fileSystem: fileSystem);

        await runner.RunAsync(Request(format: OutputFormat.Json, outputPath: "/out/inspect.json"), CancellationToken.None);

        fileSystem.ReadText("/out/inspect.json").Should().Contain("\"name\": \"demo\"");
    }

    [Fact]
    public async Task OutputPathWritesTheSummaryToThatFile()
    {
        // Asserting that nothing reached stdout would mean redirecting Console.Out, which is process-global
        // state that xUnit's parallel classes would corrupt. The file's content is the behaviour that
        // matters; stdout is covered by the runner's own branch on OutputPath being null.
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, out _, fileSystem: fileSystem);

        await runner.RunAsync(Request(outputPath: "/out/inspect.txt"), CancellationToken.None);

        fileSystem.FileExists("/out/inspect.txt").Should().BeTrue();
        fileSystem.ReadText("/out/inspect.txt").Should().Contain("Skill: demo");
    }

    [Fact]
    public async Task WithNoOutputPathNothingIsWrittenToTheFileSystem()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, out _, fileSystem: fileSystem);

        await runner.RunAsync(Request(), CancellationToken.None);

        fileSystem.FileExists("/out/inspect.txt").Should().BeFalse();
    }

    [Fact]
    public async Task InspectNeverFailsOnInformationalFindings()
    {
        // A skill that ships a script and external URLs is still just described, not blocked.
        var runner = Build(out _, out _, inspection: new SkillInspection(
            "demo",
            "/skills/demo",
            "1.0.0",
            [],
            ["https://example.com"],
            ["Runs a script"],
            [],
            [Diagnostic.Warning(DiagnosticCodes.ContainsScript, "ships a script")]));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    private static InspectRequest Request(
        string format = OutputFormat.Console,
        string? outputPath = null,
        bool showFiles = false,
        bool showLinks = false,
        bool showPermissions = false) =>
        new("/skills/demo", format, outputPath, showFiles, showLinks, showPermissions, new ReportRenderOptions());

    private static InspectCommandRunner Build(
        out RecordingRenderer renderer,
        out StubInspector inspector,
        FakeFileSystem? fileSystem = null,
        Diagnostic? loadFailure = null,
        SkillInspection? inspection = null)
    {
        renderer = new RecordingRenderer();
        inspector = new StubInspector(inspection ?? DefaultInspection());

        return new InspectCommandRunner(
            new StubLoader(loadFailure),
            inspector,
            fileSystem ?? new FakeFileSystem(),
            renderer);
    }

    private static SkillInspection DefaultInspection() => new(
        "demo",
        "/skills/demo",
        "1.0.0",
        [],
        [],
        [],
        [],
        []);

    private sealed class StubLoader(Diagnostic? failure = null) : ISkillLoader
    {
        public Task<OperationResult<SkillDefinition>> LoadAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(failure is null
                ? OperationResult<SkillDefinition>.Success(CreateSkill())
                : OperationResult<SkillDefinition>.Failure(failure));

        private static SkillDefinition CreateSkill() =>
            new(
                "demo",
                "Use this skill when testing inspect.",
                "/skills/demo",
                "/skills/demo/SKILL.md",
                SkillFrontmatter.Empty(1, 2),
                [],
                "# Demo",
                BodyStartLine: 4,
                SkillFileLineCount: 4);
    }

    private sealed class StubInspector(SkillInspection inspection) : ISkillInspector
    {
        public ValueTask<SkillInspection> InspectAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(inspection);
    }

    private sealed class RecordingRenderer : IValidationReportRenderer
    {
        internal ValidationReport? Rendered { get; private set; }

        public void Render(ValidationReport report, ReportRenderOptions options) => Rendered = report;
    }
}
