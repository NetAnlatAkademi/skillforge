using SkillForge.Application.Abstractions;
using SkillForge.Application.Packaging;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Packaging;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Tests;

/// <summary>
/// Validation is a gate in front of packaging: an invalid skill is refused unless the caller explicitly
/// skips the gate, and the packager is never invoked when it should not be.
/// </summary>
public sealed class PackCommandRunnerTests
{
    [Fact]
    public async Task AValidSkillExitsZeroAndReportsThePackagePaths()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            var runner = Build(out _, out var packager);

            var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

            exitCode.Should().Be(0);
            packager.WasCalled.Should().BeTrue();
            var output = captured.ToString();
            output.Should().Contain("/artifacts/demo.skillpkg");
            output.Should().Contain("/artifacts/demo.sha256");
            output.Should().Contain("/artifacts/demo.manifest.json");
            output.Should().Contain("deadbeef");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task AValidationErrorExitsOneAndNeverInvokesThePackager()
    {
        var runner = Build(
            out _,
            out var packager,
            validationFindings: [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        packager.WasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SkipValidationPackagesDespiteErrors()
    {
        var runner = Build(
            out _,
            out var packager,
            validationFindings: [Diagnostic.Error(DiagnosticCodes.NameMissing, "no name")]);

        var exitCode = await runner.RunAsync(Request(skipValidation: true), CancellationToken.None);

        exitCode.Should().Be(0);
        packager.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnloadableSkillExitsOne()
    {
        var runner = Build(
            out var renderer,
            out var packager,
            loadFailure: Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        packager.WasCalled.Should().BeFalse();
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task APackagerFailureExitsOne()
    {
        var runner = Build(out var renderer, out _, packagerFailure:
            Diagnostic.Error(DiagnosticCodes.SkillFileTooLong, "could not write archive"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileTooLong);
    }

    private static PackRequest Request(bool skipValidation = false) =>
        new("/skills/demo", "/artifacts", VersionOverride: null, skipValidation, new ReportRenderOptions());

    private static PackCommandRunner Build(
        out RecordingRenderer renderer,
        out StubPackager packager,
        Diagnostic? loadFailure = null,
        Diagnostic? packagerFailure = null,
        IReadOnlyList<Diagnostic>? validationFindings = null)
    {
        renderer = new RecordingRenderer();
        packager = new StubPackager(packagerFailure);

        return new PackCommandRunner(
            new StubLoader(loadFailure),
            new StubValidator(validationFindings ?? []),
            packager,
            renderer);
    }

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
                "Use this skill when testing pack.",
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

    private sealed class StubPackager(Diagnostic? failure = null) : ISkillPackager
    {
        internal bool WasCalled { get; private set; }

        public Task<OperationResult<SkillPackage>> PackAsync(
            SkillDefinition skill,
            string outputDirectory,
            string? versionOverride,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            if (failure is not null)
            {
                return Task.FromResult(OperationResult<SkillPackage>.Failure(failure));
            }

            return Task.FromResult(OperationResult<SkillPackage>.Success(new SkillPackage(
                skill.Name,
                "1.0.0",
                $"{outputDirectory}/demo.skillpkg",
                $"{outputDirectory}/demo.sha256",
                $"{outputDirectory}/demo.manifest.json",
                "deadbeef",
                [],
                DateTimeOffset.UnixEpoch)));
        }
    }

    private sealed class RecordingRenderer : IValidationReportRenderer
    {
        internal ValidationReport? Rendered { get; private set; }

        public void Render(ValidationReport report, ReportRenderOptions options) => Rendered = report;
    }
}
