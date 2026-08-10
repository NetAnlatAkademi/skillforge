using SkillForge.Application.Abstractions;
using SkillForge.Application.Policy;
using SkillForge.Cli.Commands;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Policy;
using SkillForge.Reporting;

namespace SkillForge.Cli.Tests;

/// <summary>
/// The exit codes and the output shapes. The one that matters most is the default: a diff describes, and only
/// <c>--fail-on-weakening</c> turns it into a gate — whether a wider policy is acceptable is the organisation's
/// decision, not SkillForge's.
/// </summary>
public sealed class PolicyDiffCommandRunnerTests
{
    [Fact]
    public async Task AnUnchangedPolicyExitsZeroAndSaysSo()
    {
        var files = new FakeFileSystem();
        var runner = Build(PolicyDocument.Empty, PolicyDocument.Empty, files, out _);

        var exitCode = await runner.RunAsync(
            Request(outputPath: "/out/diff.txt"),
            CancellationToken.None);

        exitCode.Should().Be(0);
        files.ReadText("/out/diff.txt").Should().Contain("decide the same things");
    }

    [Fact]
    public async Task ARelaxedPolicyStillExitsZeroWithoutTheFlag()
    {
        var runner = Build(Strict(), PolicyDocument.Empty, new FakeFileSystem(), out _);

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ARelaxedPolicyExitsOneWhenAskedToFail()
    {
        var runner = Build(Strict(), PolicyDocument.Empty, new FakeFileSystem(), out _);

        var exitCode = await runner.RunAsync(
            Request(failOnWeakening: true),
            CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ATightenedPolicyPassesEvenWhenAskedToFail()
    {
        var runner = Build(PolicyDocument.Empty, Strict(), new FakeFileSystem(), out _);

        var exitCode = await runner.RunAsync(
            Request(failOnWeakening: true),
            CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task APolicyThatCouldNotBeReadFailsAndComparesNothing()
    {
        var runner = Build(
            null,
            PolicyDocument.Empty,
            new FakeFileSystem(),
            out var renderer,
            failure: Diagnostic.Error(DiagnosticCodes.PolicyNotParsable, "the policy is not valid YAML"));

        var exitCode = await runner.RunAsync(Request(), CancellationToken.None);

        exitCode.Should().Be(1);
        renderer.Rendered!.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyNotParsable);
    }

    [Fact]
    public async Task JsonCarriesTheDeltaAndTheFindings()
    {
        var files = new FakeFileSystem();
        var runner = Build(Strict(), Widened(), files, out _);

        await runner.RunAsync(
            Request(format: OutputFormat.Json, outputPath: "/out/diff.json"),
            CancellationToken.None);

        var json = files.ReadText("/out/diff.json");

        json.Should().Contain("\"weakened\": true");
        json.Should().Contain(DiagnosticCodes.McpPolicyRemoteDomainPermitted);
        json.Should().Contain("newlyPermitted");
    }

    [Fact]
    public async Task SarifCarriesOnlyWhatWasRelaxed()
    {
        var files = new FakeFileSystem();
        var runner = Build(Strict(), Widened(), files, out _);

        await runner.RunAsync(
            Request(format: OutputFormat.Sarif, outputPath: "/out/diff.sarif"),
            CancellationToken.None);

        files.ReadText("/out/diff.sarif").Should().Contain(DiagnosticCodes.McpPolicyRemoteDomainPermitted);
    }

    private static PolicyDocument Strict() => PolicyDocument.Empty with
    {
        Skills = new PolicySkills(RequireLicense: true, MaxSkillFileLines: 500),
        Mcp = new PolicyMcp([], false, McpPolicyDefault.Deny, [], []),
    };

    private static PolicyDocument Widened() => Strict() with
    {
        Mcp = new PolicyMcp(
            [],
            false,
            McpPolicyDefault.Deny,
            [McpPolicyRule.Url("https://mcp.vendor.com/*")],
            []),
    };

    private static PolicyDiffRequest Request(
        bool failOnWeakening = false,
        string format = OutputFormat.Console,
        string? outputPath = null) =>
        new(
            "before/policy.yaml",
            "after/policy.yaml",
            failOnWeakening,
            format,
            outputPath,
            new ReportRenderOptions(Quiet: true));

    private static PolicyDiffCommandRunner Build(
        PolicyDocument? before,
        PolicyDocument? after,
        FakeFileSystem files,
        out RecordingRenderer renderer,
        Diagnostic? failure = null)
    {
        renderer = new RecordingRenderer();

        return new PolicyDiffCommandRunner(
            new PairedPolicyReader(before, after, failure),
            files,
            renderer,
            [new JsonReportSerializer(), new SarifReportSerializer()]);
    }

    /// <summary>Answers with the "before" document first and the "after" document second.</summary>
    private sealed class PairedPolicyReader(PolicyDocument? before, PolicyDocument? after, Diagnostic? failure)
        : IPolicyReader
    {
        private int _calls;

        public Task<OperationResult<PolicyDocument>> ReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            var policy = _calls++ == 0 ? before : after;

            return Task.FromResult(policy is null
                ? OperationResult<PolicyDocument>.Failure(
                    failure ?? Diagnostic.Error(DiagnosticCodes.PolicyNotParsable, "unreadable"))
                : OperationResult<PolicyDocument>.Success(policy));
        }
    }
}
