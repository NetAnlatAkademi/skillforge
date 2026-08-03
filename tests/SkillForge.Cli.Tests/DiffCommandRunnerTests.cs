using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// What <c>diff</c> writes, and what it exits with. The SARIF cases matter most: a code-scanning upload of an
/// invalid document fails the whole workflow, and one carrying results that are not findings misleads a reviewer.
/// </summary>
public sealed class DiffCommandRunnerTests
{
    [Fact]
    public async Task SarifOverAnUnchangedSkillIsValidAndCarriesNoResults()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(fileSystem, before: Skill(), after: Skill());

        var exitCode = await runner.RunAsync(
            Request(OutputFormat.Sarif, "/out/diff.sarif"),
            CancellationToken.None);

        exitCode.Should().Be(0);

        var document = JsonDocument.Parse(fileSystem.ReadText("/out/diff.sarif")).RootElement;
        document.GetProperty("version").GetString().Should().Be("2.1.0");
        document.GetProperty("runs")[0].GetProperty("results").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SarifCarriesANewPermissionAsAResult()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(
            fileSystem,
            before: Skill(tools: ["filesystem.read"]),
            after: Skill(tools: ["filesystem.read", "shell.execute"]));

        await runner.RunAsync(Request(OutputFormat.Sarif, "/out/diff.sarif"), CancellationToken.None);

        var results = JsonDocument.Parse(fileSystem.ReadText("/out/diff.sarif"))
            .RootElement.GetProperty("runs")[0].GetProperty("results");

        results.EnumerateArray()
            .Select(result => result.GetProperty("ruleId").GetString())
            .Should().Contain(DiagnosticCodes.PermissionAdded);
    }

    [Fact]
    public async Task ANewErrorStillFailsWhateverTheFormatIs()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(
            fileSystem,
            before: Skill(),
            after: Skill(),
            afterFindings: [Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 3)]);

        var exitCode = await runner.RunAsync(
            Request(OutputFormat.Sarif, "/out/diff.sarif"),
            CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task JsonIsUnaffectedBySarifBeingAvailable()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(fileSystem, before: Skill(), after: Skill());

        await runner.RunAsync(Request(OutputFormat.Json, "/out/diff.json"), CancellationToken.None);

        fileSystem.ReadText("/out/diff.json").Should().Contain("\"hasChanges\": false");
    }

    private static DiffRequest Request(string format, string? outputPath) =>
        new("/skills/before", "/skills/after", format, outputPath, FailOnChange: false, new ReportRenderOptions());

    private static DiffCommandRunner Build(
        FakeFileSystem fileSystem,
        SkillDefinition before,
        SkillDefinition after,
        IReadOnlyList<Diagnostic>? afterFindings = null) =>
        new(
            new StubLoader(before, after),
            new StubInspector(),
            new StubValidator(afterFindings ?? []),
            fileSystem,
            new RecordingRenderer(),
            [new JsonReportSerializer(), new SarifReportSerializer()]);

    private static SkillDefinition Skill(IReadOnlyList<string>? tools = null) =>
        new(
            "demo",
            "Use this skill when testing diff.",
            "/skills/demo",
            "/skills/demo/SKILL.md",
            SkillFrontmatter.Empty(1, 2) with { AllowedTools = [.. tools ?? []] },
            [],
            "# Demo",
            BodyStartLine: 4,
            SkillFileLineCount: 4);

    private sealed class StubLoader(SkillDefinition before, SkillDefinition after) : ISkillLoader
    {
        private bool _served;

        public Task<OperationResult<SkillDefinition>> LoadAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var skill = _served ? after : before;
            _served = true;

            return Task.FromResult(OperationResult<SkillDefinition>.Success(skill));
        }
    }

    private sealed class StubInspector : ISkillInspector
    {
        public ValueTask<SkillInspection> InspectAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SkillInspection(
                skill.Name,
                skill.DirectoryPath,
                skill.Frontmatter.Version,
                skill.Resources,
                [],
                [],
                skill.Frontmatter.AllowedTools,
                []));
    }

    /// <summary>Findings on the later side only, so the diff has something new to report.</summary>
    private sealed class StubValidator(IReadOnlyList<Diagnostic> afterFindings) : ISkillValidator
    {
        private bool _served;

        public Task<ValidationReport> ValidateAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken = default)
        {
            var findings = _served ? afterFindings : [];
            _served = true;

            return Task.FromResult(ValidationReport.For(skill, findings));
        }
    }
}
