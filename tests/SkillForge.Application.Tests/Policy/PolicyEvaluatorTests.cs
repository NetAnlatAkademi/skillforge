using SkillForge.Application.Policy;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Policy;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Policy;

/// <summary>
/// Policy is the one place SkillForge judges rather than describes, and it judges only what somebody wrote down.
/// Half of these tests are about silence: a policy that says nothing must produce nothing.
/// </summary>
public sealed class PolicyEvaluatorTests
{
    [Fact]
    public void AnEmptyPolicyFindsNothingEvenAgainstASkillThatDoesEverything()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            PolicyDocument.Empty,
            Subject(scripts: ["scripts/run.ps1"], urls: ["https://api.example.com/v1"], license: null));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ShellForbiddenFiresOnASkillThatShipsAScript()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(false, null, [], null)),
            Subject(scripts: ["scripts/run.ps1"]));

        var finding = diagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.PolicyShellForbidden).Subject;

        finding.Severity.Should().Be(DiagnosticSeverity.Error);
        finding.Message.Should().Contain("scripts/run.ps1");
    }

    [Fact]
    public void ShellForbiddenFiresOnADeclaredShellCommandWithNoScriptInSight()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(false, null, [], null)),
            Subject(declaredShellCommands: ["pwsh ./scripts/run.ps1"]));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyShellForbidden)
            .Which.Message.Should().Contain("pwsh ./scripts/run.ps1");
    }

    [Fact]
    public void ShellAllowedFindsNothingOnTheSameSkill()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(true, null, [], null)),
            Subject(scripts: ["scripts/run.ps1"]));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ShellForbiddenFindsNothingOnASkillWithNoShellAnywhere()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(false, null, [], null)),
            Subject());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void FilesystemWriteForbiddenFiresOnADeclaredWritePermission()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(null, false, [], null)),
            Subject(tools: ["filesystem.write"]));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyFilesystemWriteForbidden);
    }

    [Fact]
    public void FilesystemReadIsNotAWrite()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(null, false, [], null)),
            Subject(tools: ["filesystem.read"]));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ADomainOutsideTheAllowListIsAViolationNamingTheHost()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(null, null, [], ["learn.microsoft.com"])),
            Subject(urls: ["https://learn.microsoft.com/a", "https://api.example.com/v1"]));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyDomainNotAllowed)
            .Which.Message.Should().Contain("api.example.com");
    }

    [Fact]
    public void AnEmptyAllowListForbidsEveryHostRatherThanMeaningSilence()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(null, null, [], [])),
            Subject(urls: ["https://learn.microsoft.com/a"]));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyDomainNotAllowed);
    }

    [Fact]
    public void HostsAreComparedWithoutCaseAndWithoutTheirPaths()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(permissions: new PolicyPermissions(null, null, [], ["Learn.Microsoft.COM"])),
            Subject(urls: ["https://learn.microsoft.com/a/deep/path?q=1"]));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void RequireCommitShaFiresWhenTheSourceCannotBeIdentified()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(provenance: new PolicyProvenance(true, false)),
            Subject());

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyProvenanceMissing);
    }

    [Fact]
    public void RequireCommitShaFiresOnADirtyWorkingTreeEvenThoughACommitIsNamed()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(provenance: new PolicyProvenance(true, false)),
            Subject(provenance: new SkillProvenance(
                "https://github.com/example/skills.git",
                "abc123",
                "skills/demo",
                WorkingTreeIsDirty: true,
                "26.215.1",
                DateTimeOffset.UnixEpoch)));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyProvenanceMissing)
            .Which.Message.Should().Contain("uncommitted");
    }

    [Fact]
    public void RequireCommitShaPassesOnACleanTraceableCheckout()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(provenance: new PolicyProvenance(true, false)),
            Subject(provenance: new SkillProvenance(
                "https://github.com/example/skills.git",
                "abc123",
                "skills/demo",
                WorkingTreeIsDirty: false,
                "26.215.1",
                DateTimeOffset.UnixEpoch)));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void RequireLicenseFiresWhenNoneIsDeclared()
    {
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(skills: new PolicySkills(true, null)),
            Subject(license: null));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyLicenseMissing);
    }

    [Fact]
    public void MaxSkillFileLinesFiresOnlyAboveTheLimit()
    {
        var atTheLimit = PolicyEvaluator.Evaluate(
            Policy(skills: new PolicySkills(false, 500)),
            Subject(skillFileLines: 500));

        var overIt = PolicyEvaluator.Evaluate(
            Policy(skills: new PolicySkills(false, 500)),
            Subject(skillFileLines: 501));

        atTheLimit.Should().BeEmpty();
        overIt.Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicySkillFileTooLong);
    }

    [Fact]
    public void ASuppressionWithAReasonSilencesTheRule()
    {
        var policy = Policy(
            permissions: new PolicyPermissions(false, null, [], null),
            suppressions: [new PolicySuppression(DiagnosticCodes.PolicyShellForbidden, null, "TICKET-123")]);

        PolicyEvaluator.Evaluate(policy, Subject(scripts: ["scripts/run.ps1"])).Should().BeEmpty();
    }

    [Fact]
    public void ASuppressionScopedToAnotherSkillDoesNotApply()
    {
        var policy = Policy(
            permissions: new PolicyPermissions(false, null, [], null),
            suppressions:
            [
                new PolicySuppression(DiagnosticCodes.PolicyShellForbidden, "another-skill", "TICKET-123"),
            ]);

        PolicyEvaluator.Evaluate(policy, Subject(scripts: ["scripts/run.ps1"]))
            .Should().ContainSingle(d => d.Code == DiagnosticCodes.PolicyShellForbidden);
    }

    [Fact]
    public void EveryFindingCarriesTheEvidenceItWasBasedOn()
    {
        // The whole tool's contract: a finding a reader cannot check is a finding they have to take on trust.
        var diagnostics = PolicyEvaluator.Evaluate(
            Policy(
                permissions: new PolicyPermissions(false, false, [], []),
                provenance: new PolicyProvenance(true, false),
                skills: new PolicySkills(true, 10)),
            Subject(
                scripts: ["scripts/run.ps1"],
                urls: ["https://api.example.com/v1"],
                tools: ["filesystem.write"],
                license: null,
                skillFileLines: 400));

        diagnostics.Should().HaveCountGreaterThan(4);
        diagnostics.Should().AllSatisfy(finding =>
        {
            finding.Suggestion.Should().NotBeNullOrWhiteSpace();
            finding.FilePath.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void RejectsAMissingPolicyOrSubject()
    {
        var noPolicy = () => PolicyEvaluator.Evaluate(null!, Subject());
        var noSubject = () => PolicyEvaluator.Evaluate(PolicyDocument.Empty, null!);

        noPolicy.Should().Throw<ArgumentNullException>();
        noSubject.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ARuleThisCommandCannotObserveIsReportedRatherThanTreatedAsPassed()
    {
        var notEvaluated = PolicyEvaluator.DescribeUnevaluatedRules(Policy(
            permissions: new PolicyPermissions(null, true, ["./reports/**"], null),
            provenance: new PolicyProvenance(false, true),
            mcp: new PolicyMcp(["2026-07-28"], true)));

        notEvaluated.Should().HaveCount(3);
        notEvaluated.Should().AllSatisfy(finding =>
        {
            finding.Code.Should().Be(DiagnosticCodes.PolicyRuleNotEvaluated);
            finding.Severity.Should().Be(DiagnosticSeverity.Info);
        });
    }

    [Fact]
    public void APolicyWithNothingUnobservableSaysNothing()
    {
        PolicyEvaluator.DescribeUnevaluatedRules(PolicyDocument.Empty).Should().BeEmpty();
    }

    private static PolicyDocument Policy(
        PolicyPermissions? permissions = null,
        PolicyProvenance? provenance = null,
        PolicySkills? skills = null,
        PolicyMcp? mcp = null,
        IReadOnlyList<PolicySuppression>? suppressions = null) =>
        new(
            permissions ?? new PolicyPermissions(null, null, [], null),
            provenance ?? new PolicyProvenance(false, false),
            skills ?? new PolicySkills(false, null),
            mcp,
            suppressions ?? []);

    private static PolicySubject Subject(
        IReadOnlyList<string>? scripts = null,
        IReadOnlyList<string>? urls = null,
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? declaredShellCommands = null,
        string? license = "MIT",
        int skillFileLines = 40,
        SkillProvenance? provenance = null)
    {
        var skill = new SkillBuilder()
            .WithName("demo-skill")
            .WithLicense(license)
            .WithAllowedTools([.. tools ?? []])
            .WithResources([.. Enumerable.Repeat("SKILL.md", 1).Concat(scripts ?? [])])
            .WithSkillFileLineCount(skillFileLines)
            .Build();

        var capabilities = new List<string> { SkillCapabilities.FilesystemRead };
        if (scripts is { Count: > 0 })
        {
            capabilities.Add(SkillCapabilities.ShellExecution);
        }

        var inspection = new SkillInspection(
            skill.Name,
            skill.DirectoryPath,
            skill.Frontmatter.Version,
            skill.Resources,
            urls ?? [],
            capabilities,
            skill.Frontmatter.AllowedTools,
            []);

        var configuration = SkillConfiguration.Default with
        {
            ShellAllowed = declaredShellCommands ?? [],
        };

        return new PolicySubject(
            skill,
            inspection,
            configuration,
            provenance ?? new SkillProvenance(null, null, null, false, "26.215.1", DateTimeOffset.UnixEpoch));
    }
}
