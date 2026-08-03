using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Policy;
using SkillForge.Application.Provenance;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Policy;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Skills;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// The exit codes, and the one case that matters most: a policy that could not be read must not produce a green
/// build. A run that checked nothing and said everything was fine is worse than no run at all.
/// </summary>
public sealed class PolicyCheckCommandRunnerTests
{
    [Fact]
    public async Task ASkillWithinPolicyExitsZero()
    {
        var runner = Build(PolicyDocument.Empty, out _);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task AViolationExitsOne()
    {
        var policy = PolicyDocument.Empty with
        {
            Skills = new PolicySkills(RequireLicense: true, MaxSkillFileLines: null),
        };

        var runner = Build(policy, out _);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task AnUnreadablePolicyExitsOneAndChecksNothing()
    {
        var runner = Build(
            policy: null,
            out var renderer,
            policyFailure: Diagnostic.Error(DiagnosticCodes.PolicyNotParsable, "the policy is not valid YAML"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyNotParsable);
    }

    [Fact]
    public async Task ARuleThatCouldNotBeCheckedIsReportedAgainstThePolicyFile()
    {
        var policy = PolicyDocument.Empty with { Mcp = new PolicyMcp(["2026-07-28"], true) };

        var fileSystem = new FakeFileSystem();
        var runner = Build(policy, out _, fileSystem: fileSystem);

        var exitCode = await runner.RunAsync(
            Request(format: OutputFormat.Json, outputPath: "/out/policy.json"),
            CancellationToken.None);

        // Info, so it says what was not checked without failing the build over it.
        exitCode.Should().Be(0);
        fileSystem.ReadText("/out/policy.json").Should().Contain(DiagnosticCodes.PolicyRuleNotEvaluated);
    }

    [Fact]
    public async Task ASkillThatWillNotLoadIsReportedRatherThanPassedThroughTheGate()
    {
        var runner = Build(
            PolicyDocument.Empty,
            out _,
            loadFailure: Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "no SKILL.md"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task SarifIsWrittenWhenAskedFor()
    {
        var policy = PolicyDocument.Empty with
        {
            Skills = new PolicySkills(RequireLicense: true, MaxSkillFileLines: null),
        };

        var fileSystem = new FakeFileSystem();
        var runner = Build(policy, out _, fileSystem: fileSystem);

        await runner.RunAsync(
            Request(format: OutputFormat.Sarif, outputPath: "/out/policy.sarif"),
            CancellationToken.None);

        fileSystem.ReadText("/out/policy.sarif").Should().Contain(DiagnosticCodes.PolicyLicenseMissing);
    }

    private static PolicyCheckRequest Request(
        string format = OutputFormat.Console,
        string? outputPath = null) =>
        new("/skills/demo", ".skillforge/policy.yaml", format, outputPath, new ReportRenderOptions(Quiet: true));

    private static PolicyCheckCommandRunner Build(
        PolicyDocument? policy,
        out RecordingRenderer renderer,
        FakeFileSystem? fileSystem = null,
        Diagnostic? policyFailure = null,
        Diagnostic? loadFailure = null)
    {
        renderer = new RecordingRenderer();
        var files = fileSystem ?? new FakeFileSystem();
        files.WithFile("/skills/demo/SKILL.md", "---\nname: demo\n---\n");

        return new PolicyCheckCommandRunner(
            new StubPolicyReader(policy, policyFailure),
            new StubLoader(loadFailure),
            new StubInspector(),
            new StubDiscovery(),
            new StubProvenanceReader(),
            files,
            renderer,
            new ReportOutput(files, renderer, [new JsonReportSerializer(), new SarifReportSerializer()]));
    }

    private sealed class StubPolicyReader(PolicyDocument? policy, Diagnostic? failure) : IPolicyReader
    {
        public Task<OperationResult<PolicyDocument>> ReadAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(failure is not null || policy is null
                ? OperationResult<PolicyDocument>.Failure(
                    failure ?? Diagnostic.Error(DiagnosticCodes.PolicyNotParsable, "no policy"))
                : OperationResult<PolicyDocument>.Success(policy));
    }

    private sealed class StubLoader(Diagnostic? failure) : ISkillLoader
    {
        public Task<OperationResult<SkillDefinition>> LoadAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(failure is null
                ? OperationResult<SkillDefinition>.Success(new SkillDefinition(
                    "demo",
                    "Use this skill when testing policy check.",
                    "/skills/demo",
                    "/skills/demo/SKILL.md",
                    SkillFrontmatter.Empty(1, 2),
                    [],
                    "# Demo",
                    BodyStartLine: 4,
                    SkillFileLineCount: 4))
                : OperationResult<SkillDefinition>.Failure(failure));
    }

    private sealed class StubInspector : ISkillInspector
    {
        public ValueTask<SkillInspection> InspectAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SkillInspection(
                skill.Name,
                skill.DirectoryPath,
                null,
                skill.Resources,
                [],
                [],
                skill.Frontmatter.AllowedTools,
                []));
    }

    private sealed class StubDiscovery : ISkillDiscovery
    {
        public IReadOnlyList<string> FindSkillDirectories(string rootPath) => [];
    }

    private sealed class StubProvenanceReader : IProvenanceReader
    {
        public ValueTask<SkillProvenance> ReadAsync(
            string skillDirectory,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SkillProvenance(
                null,
                null,
                null,
                false,
                "26.215.1",
                DateTimeOffset.UnixEpoch));
    }
}
