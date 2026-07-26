using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Tests;

/// <summary>
/// The exit code is the CLI's real contract with CI, so each situation that produces one gets a test.
/// </summary>
public sealed class ValidateCommandRunnerTests
{
    private static readonly ReportRenderOptions Defaults = new();

    [Fact]
    public async Task ACleanSkillExitsZero()
    {
        var runner = Build(out var renderer);

        var exitCode = await runner.RunAsync("/skills/demo", strict: false, Defaults, CancellationToken.None);

        exitCode.Should().Be(0);
        renderer.Rendered.Should().NotBeNull();
    }

    [Fact]
    public async Task AnErrorExitsOne()
    {
        var runner = Build(out _, validationFindings:
            [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        var exitCode = await runner.RunAsync("/skills/demo", strict: false, Defaults, CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task AWarningExitsZeroUnlessStrict()
    {
        var findings = new[] { Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license") };

        var lenient = await Build(out _, validationFindings: findings)
            .RunAsync("/skills/demo", strict: false, Defaults, CancellationToken.None);
        var strict = await Build(out _, validationFindings: findings)
            .RunAsync("/skills/demo", strict: true, Defaults, CancellationToken.None);

        lenient.Should().Be(0);
        strict.Should().Be(1);
    }

    [Fact]
    public async Task ASkillThatCannotBeLoadedExitsOneAndIsStillReported()
    {
        var runner = Build(out var renderer, loadFailure:
            Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync("/nowhere", strict: false, Defaults, CancellationToken.None);

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

        var exitCode = await runner.RunAsync("/skills/demo", strict: false, Defaults, CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().Equal(DiagnosticCodes.DuplicateMetadataField, DiagnosticCodes.LicenseMissing);
        renderer.Rendered.Summary.Errors.Should().Be(1);
        renderer.Rendered.Summary.Warnings.Should().Be(1);
    }

    [Fact]
    public void RejectsMissingDependencies()
    {
        var act = () => new ValidateCommandRunner(null!, null!, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ValidateCommandRunner Build(
        out RecordingRenderer renderer,
        Diagnostic? loadFailure = null,
        IReadOnlyList<Diagnostic>? loadDiagnostics = null,
        IReadOnlyList<Diagnostic>? validationFindings = null)
    {
        renderer = new RecordingRenderer();
        return new ValidateCommandRunner(
            new StubLoader(loadFailure, loadDiagnostics ?? []),
            new StubValidator(validationFindings ?? []),
            renderer);
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

    private sealed class RecordingRenderer : IValidationReportRenderer
    {
        internal ValidationReport? Rendered { get; private set; }

        public void Render(ValidationReport report, ReportRenderOptions options) => Rendered = report;
    }
}
