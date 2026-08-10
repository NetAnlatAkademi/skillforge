using SkillForge.Application.Policy;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Tests.Policy;

/// <summary>
/// A policy diff reports relaxations. Half of these tests are about the other direction: a policy that got stricter
/// must produce nothing, or the command becomes noise and the finding that mattered gets skipped with the rest.
/// </summary>
public sealed class PolicyDifferTests
{
    [Fact]
    public void TwoIdenticalPoliciesDifferInNothing()
    {
        var policy = PolicyDocument.Empty with
        {
            Mcp = Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("https://mcp.company.com/*")]),
        };

        var diff = Compare(policy, policy);

        diff.HasChanges.Should().BeFalse();
        PolicyChangeDiagnostics.From(diff).Should().BeEmpty();
    }

    [Fact]
    public void AWildcardThatSwallowsAnExistingRuleIsReportedAsAWidening()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("api.company.com")]));
        var after = WithMcp(Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("*.company.com")]));

        var diff = Compare(before, after);

        diff.Mcp.Widenings.Should().ContainSingle()
            .Which.Generalises.Pattern.Should().Be("api.company.com");

        var finding = PolicyChangeDiagnostics.From(diff).Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.McpPolicyWildcardExpanded);
        finding.Message.Should().Contain("api.company.com").And.Contain("*.company.com");
    }

    [Fact]
    public void ANewRemoteEndpointIsReportedOnce()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny));
        var after = WithMcp(Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("https://mcp.vendor.com/*")]));

        PolicyChangeDiagnostics.From(Compare(before, after)).Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpPolicyRemoteDomainPermitted);
    }

    [Fact]
    public void ANewLocalCommandIsReportedAsSuch()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny));
        var after = WithMcp(Mcp(
            McpPolicyDefault.Deny,
            allow: [McpPolicyRule.Command("npx", "@vendor/mcp")]));

        var finding = PolicyChangeDiagnostics.From(Compare(before, after)).Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpPolicyLocalCommandPermitted);
        finding.Message.Should().Contain("npx @vendor/mcp").And.Contain("an allow rule was added");
    }

    /// <summary>
    /// The edit that reads as a deletion and behaves as a permission. Both halves of the message matter: what is
    /// now reachable, and that it happened by a deny disappearing rather than an allow appearing.
    /// </summary>
    [Fact]
    public void ARemovedDenyIsReportedAsAPermission()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny, deny: [McpPolicyRule.Url("http://*")]));
        var after = WithMcp(Mcp(McpPolicyDefault.Deny));

        var finding = PolicyChangeDiagnostics.From(Compare(before, after)).Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpPolicyRemoteDomainPermitted);
        finding.Message.Should().Contain("a deny rule was removed");
    }

    [Fact]
    public void ATightenedPolicyReportsNothing()
    {
        var before = WithMcp(Mcp(
            McpPolicyDefault.Deny,
            allow: [McpPolicyRule.Url("*.company.com"), McpPolicyRule.Command("npx")]));

        var after = WithMcp(Mcp(
            McpPolicyDefault.Deny,
            allow: [McpPolicyRule.Url("api.company.com")],
            deny: [McpPolicyRule.Url("http://*")]));

        var diff = Compare(before, after);

        diff.HasChanges.Should().BeTrue();
        PolicyChangeDiagnostics.From(diff).Should().BeEmpty();
    }

    [Fact]
    public void ADefaultThatStopsDenyingIsAnError()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny));
        var after = WithMcp(Mcp(McpPolicyDefault.NotDeclared));

        var finding = PolicyChangeDiagnostics.From(Compare(before, after)).Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpPolicyFailOpen);
        finding.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ADefaultThatStartsDenyingIsNotAFinding()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Allow));
        var after = WithMcp(Mcp(McpPolicyDefault.Deny));

        PolicyChangeDiagnostics.From(Compare(before, after)).Should().BeEmpty();
    }

    [Fact]
    public void DeletingTheWholeMcpSectionReportsEveryDenyItCarried()
    {
        var before = WithMcp(Mcp(McpPolicyDefault.Deny, deny: [McpPolicyRule.Url("http://*")]));
        var after = PolicyDocument.Empty;

        var findings = PolicyChangeDiagnostics.From(Compare(before, after));

        findings.Should().Contain(finding => finding.Code == DiagnosticCodes.McpPolicyFailOpen);
        findings.Should().Contain(finding => finding.Code == DiagnosticCodes.McpPolicyRemoteDomainPermitted);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("write")]
    [InlineData("hosts")]
    [InlineData("provenance")]
    [InlineData("license")]
    [InlineData("lines")]
    [InlineData("suppression")]
    public void EveryEnforcedRuleThatIsRelaxedIsReported(string rule)
    {
        var (before, after) = Relax(rule);

        PolicyChangeDiagnostics.From(Compare(before, after)).Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyRelaxed);
    }

    /// <summary>
    /// A host allow-list that disappears stops the whole check running, which no single added entry does — and it
    /// is one finding about the list, not one per host that used to be on it.
    /// </summary>
    [Fact]
    public void AHostAllowListThatDisappearsIsOneFindingAboutTheList()
    {
        var before = PolicyDocument.Empty with
        {
            Permissions = new PolicyPermissions(null, null, [], ["a.example", "b.example"]),
        };

        var finding = PolicyChangeDiagnostics.From(Compare(before, PolicyDocument.Empty))
            .Should().ContainSingle().Subject;

        finding.Message.Should().Contain("allowedDomains").And.Contain("not stated");
    }

    [Fact]
    public void ALoweredLineLimitIsNotARelaxation()
    {
        var before = PolicyDocument.Empty with { Skills = new PolicySkills(false, 1000) };
        var after = PolicyDocument.Empty with { Skills = new PolicySkills(false, 500) };

        PolicyChangeDiagnostics.From(Compare(before, after)).Should().BeEmpty();
    }

    /// <summary>
    /// A limit appearing where there was none is a rule being introduced, not one being loosened — the case an
    /// unstated limit read as zero would get exactly backwards.
    /// </summary>
    [Fact]
    public void ALineLimitIntroducedWhereThereWasNoneIsNotARelaxation()
    {
        var after = PolicyDocument.Empty with { Skills = new PolicySkills(false, 500) };

        PolicyChangeDiagnostics.From(Compare(PolicyDocument.Empty, after)).Should().BeEmpty();
    }

    [Fact]
    public void ALineLimitThatDisappearsIsARelaxation()
    {
        var before = PolicyDocument.Empty with { Skills = new PolicySkills(false, 500) };

        PolicyChangeDiagnostics.From(Compare(before, PolicyDocument.Empty)).Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyRelaxed);
    }

    /// <summary>
    /// Rules no command enforces are shown in the diff and carry no code — warning about a widened guarantee that
    /// nothing checks would be a warning about nothing.
    /// </summary>
    [Fact]
    public void AChangeToARuleNoCommandEnforcesIsShownButNotCoded()
    {
        var before = PolicyDocument.Empty with { Provenance = new PolicyProvenance(false, true) };
        var after = PolicyDocument.Empty with { Provenance = new PolicyProvenance(false, false) };

        var diff = Compare(before, after);

        diff.RequirePackageHash.Should().NotBeNull();
        PolicyChangeDiagnostics.From(diff).Should().BeEmpty();
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var noBefore = () => PolicyDiffer.Compare("a", null!, "b", PolicyDocument.Empty);
        var noAfterPath = () => PolicyDiffer.Compare("a", PolicyDocument.Empty, " ", PolicyDocument.Empty);
        var noDiff = () => PolicyChangeDiagnostics.From(null!);

        noBefore.Should().Throw<ArgumentNullException>();
        noAfterPath.Should().Throw<ArgumentException>();
        noDiff.Should().Throw<ArgumentNullException>();
    }

    private static (PolicyDocument Before, PolicyDocument After) Relax(string rule) => rule switch
    {
        "shell" => (
            PolicyDocument.Empty with { Permissions = new PolicyPermissions(false, null, [], null) },
            PolicyDocument.Empty with { Permissions = new PolicyPermissions(true, null, [], null) }),

        "write" => (
            PolicyDocument.Empty with { Permissions = new PolicyPermissions(null, false, [], null) },
            PolicyDocument.Empty with { Permissions = new PolicyPermissions(null, true, [], null) }),

        "hosts" => (
            PolicyDocument.Empty with { Permissions = new PolicyPermissions(null, null, [], ["a.example"]) },
            PolicyDocument.Empty with
            {
                Permissions = new PolicyPermissions(null, null, [], ["a.example", "b.example"]),
            }),

        "provenance" => (
            PolicyDocument.Empty with { Provenance = new PolicyProvenance(true, false) },
            PolicyDocument.Empty),

        "license" => (
            PolicyDocument.Empty with { Skills = new PolicySkills(true, null) },
            PolicyDocument.Empty),

        "lines" => (
            PolicyDocument.Empty with { Skills = new PolicySkills(false, 500) },
            PolicyDocument.Empty with { Skills = new PolicySkills(false, 1000) }),

        _ => (
            PolicyDocument.Empty,
            PolicyDocument.Empty with
            {
                Suppressions = [new PolicySuppression("SF1009", null, "approved in TICKET-1")],
            }),
    };

    private static PolicyDiff Compare(PolicyDocument before, PolicyDocument after) =>
        PolicyDiffer.Compare("before/policy.yaml", before, "after/policy.yaml", after);

    private static PolicyDocument WithMcp(PolicyMcp mcp) => PolicyDocument.Empty with { Mcp = mcp };

    private static PolicyMcp Mcp(
        McpPolicyDefault @default,
        IReadOnlyList<McpPolicyRule>? allow = null,
        IReadOnlyList<McpPolicyRule>? deny = null) =>
        new([], false, @default, allow ?? [], deny ?? []);
}
