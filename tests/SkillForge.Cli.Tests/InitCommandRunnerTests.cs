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
/// <c>init</c> creates a skill and then, unless refused up front, hands off to <c>validate</c> for the exit
/// code — so most of these assert what was written and what got refused, not the validation itself.
/// </summary>
public sealed class InitCommandRunnerTests
{
    [Fact]
    public async Task AFreshDirectoryCreatesTheSkillAndExitsZero()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(fileSystem, out _);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task AnExistingSkillFileWithoutForceExitsTwoAndWritesNothing()
    {
        var fileSystem = new FakeFileSystem().WithFile("/work/my-skill/SKILL.md");
        var runner = Build(fileSystem, out var initializer);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(2);
        initializer.WasCalled.Should().BeFalse("nothing should be written");
    }

    [Fact]
    public async Task AnExistingSkillFileWithForceProceeds()
    {
        var fileSystem = new FakeFileSystem().WithFile("/work/my-skill/SKILL.md");
        var runner = Build(fileSystem, out var initializer);

        var exitCode = await runner.RunAsync(
            Request(options: new SkillInitializationOptions("my-skill", Force: true)),
            CancellationToken.None);

        exitCode.Should().Be(0);
        initializer.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AnInvalidSkillNameExitsTwoAndReportsNameInvalid()
    {
        var fileSystem = new FakeFileSystem();
        var renderer = new RecordingRenderer();
        var runner = Build(fileSystem, out _, renderer);

        var exitCode = await runner.RunAsync(
            Request(options: new SkillInitializationOptions("Not Valid")),
            CancellationToken.None);

        exitCode.Should().Be(2);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.NameInvalid);
    }

    [Fact]
    public async Task QuietSuppressesTheCreatedFileListing()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            var runner = Build(new FakeFileSystem(), out _);

            await runner.RunAsync(
                Request(renderOptions: new ReportRenderOptions(Quiet: true)),
                CancellationToken.None);

            captured.ToString().Should().NotContain("Created skill");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static InitRequest Request(
        SkillInitializationOptions? options = null,
        ReportRenderOptions? renderOptions = null) =>
        new(
            "/work/my-skill",
            options ?? new SkillInitializationOptions("my-skill"),
            renderOptions ?? new ReportRenderOptions());

    private static InitCommandRunner Build(
        FakeFileSystem fileSystem,
        out StubInitializer initializer,
        RecordingRenderer? renderer = null)
    {
        initializer = new StubInitializer();
        renderer ??= new RecordingRenderer();

        var loader = new StubLoader();
        var validator = new StubValidator();
        var output = new ReportOutput(
            fileSystem,
            renderer,
            [new JsonReportSerializer(), new SarifReportSerializer()]);
        var validate = new ValidateCommandRunner(
            loader,
            validator,
            new NoSkillsFound(),
            fileSystem,
            output);

        return new InitCommandRunner(fileSystem, initializer, validate, renderer);
    }

    /// <summary>
    /// Init always validates exactly the skill it just created, so discovery never has anything to say here.
    /// </summary>
    private sealed class NoSkillsFound : ISkillDiscovery
    {
        public IReadOnlyList<string> FindSkillDirectories(string rootDirectory) => [];
    }

    /// <summary>Succeeds unless the name would not validate, mirroring the real initializer's own gate.</summary>
    private sealed class StubInitializer : ISkillInitializer
    {
        internal bool WasCalled { get; private set; }

        public Task<OperationResult<SkillInitializationResult>> InitializeAsync(
            string targetDirectory,
            SkillInitializationOptions options,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            if (!NameFormatIsUsable(options.Name))
            {
                return Task.FromResult(OperationResult<SkillInitializationResult>.Failure(
                    Diagnostic.Error(DiagnosticCodes.NameInvalid, "not a usable name")));
            }

            var skillFile = $"{targetDirectory}/SKILL.md";
            return Task.FromResult(OperationResult<SkillInitializationResult>.Success(
                new SkillInitializationResult(targetDirectory, [skillFile])));
        }

        private static bool NameFormatIsUsable(string name) =>
            name.Length > 0 && name.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-');
    }

    private sealed class StubLoader : ISkillLoader
    {
        public Task<OperationResult<SkillDefinition>> LoadAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult<SkillDefinition>.Success(new SkillDefinition(
                "my-skill",
                "Use this skill when testing init.",
                path,
                $"{path}/SKILL.md",
                SkillFrontmatter.Empty(1, 2),
                [],
                "# My Skill",
                BodyStartLine: 4,
                SkillFileLineCount: 4)));
    }

    private sealed class StubValidator : ISkillValidator
    {
        public Task<ValidationReport> ValidateAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken) =>
            Task.FromResult(ValidationReport.For(skill, []));
    }
}
