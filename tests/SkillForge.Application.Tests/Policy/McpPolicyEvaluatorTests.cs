using SkillForge.Application.Policy;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Tests.Policy;

/// <summary>
/// What the policy permits, and the two things it must not do: block anything when the organisation has decided
/// nothing, and invent a decision when the default is missing.
/// </summary>
public sealed class McpPolicyEvaluatorTests
{
    [Fact]
    public void ASectionThatGovernsNoServerFindsNothing()
    {
        var mcp = new PolicyMcp(["2026-07-28"], true, McpPolicyDefault.NotDeclared, [], []);

        McpPolicyEvaluator.Evaluate(mcp, Configuration(Http("anything", "https://anywhere.example"))).Should().BeEmpty();
        McpPolicyEvaluator.DescribePolicy(mcp, ".skillforge/policy.yaml").Should().BeEmpty();
    }

    [Fact]
    public void AServerNoRuleNamesIsBlockedWhenThePolicyDeniesByDefault()
    {
        var mcp = Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("https://mcp.company.com/*")]);

        var findings = McpPolicyEvaluator.Evaluate(
            mcp,
            Configuration(Http("outside", "https://mcp.elsewhere.com/sse")));

        var finding = findings.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.McpServerBlockedByPolicy);
        finding.Severity.Should().Be(DiagnosticSeverity.Error);
        finding.Message.Should().Contain("outside").And.Contain("denies by default");
    }

    [Fact]
    public void AServerAnAllowRuleNamesPassesSilently()
    {
        var mcp = Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("https://mcp.company.com/*")]);

        McpPolicyEvaluator
            .Evaluate(mcp, Configuration(Http("inside", "https://mcp.company.com/github")))
            .Should().BeEmpty();
    }

    /// <summary>
    /// The ordering that makes a deny list mean anything: a broader allow written beside it must not undo it.
    /// </summary>
    [Fact]
    public void DenyIsCheckedBeforeAllow()
    {
        var mcp = Mcp(
            McpPolicyDefault.Deny,
            allow: [McpPolicyRule.Url("http://*")],
            deny: [McpPolicyRule.Url("http://known-bad.example/*")]);

        var findings = McpPolicyEvaluator.Evaluate(
            mcp,
            Configuration(Http("bad", "http://known-bad.example/sse")));

        findings.Should().ContainSingle()
            .Which.Message.Should().Contain("the policy denies it");
    }

    [Fact]
    public void AServerPermittedOnlyByItsNameIsReported()
    {
        var mcp = Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Name("internal-notes")]);

        var finding = McpPolicyEvaluator
            .Evaluate(mcp, Configuration(Http("internal-notes", "https://anywhere.example")))
            .Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpPolicyMatchedByNameOnly);
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void AServerNamedByBothItsNameAndItsUrlIsNotReported()
    {
        var mcp = Mcp(
            McpPolicyDefault.Deny,
            allow: [McpPolicyRule.Url("https://mcp.company.com/*"), McpPolicyRule.Name("internal-notes")]);

        McpPolicyEvaluator
            .Evaluate(mcp, Configuration(Http("internal-notes", "https://mcp.company.com/notes")))
            .Should().BeEmpty();
    }

    [Fact]
    public void AnExplicitAllowDefaultIsReportedAsFailOpen()
    {
        var mcp = Mcp(McpPolicyDefault.Allow, allow: [McpPolicyRule.Url("https://mcp.company.com/*")]);

        var finding = McpPolicyEvaluator.DescribePolicy(mcp, ".skillforge/policy.yaml")
            .Should().ContainSingle().Subject;

        finding.Code.Should().Be(DiagnosticCodes.McpPolicyFailOpen);
        finding.Severity.Should().Be(DiagnosticSeverity.Error);
        finding.Message.Should().Contain("default: allow");
    }

    /// <summary>
    /// The decision recorded in the evaluator's remarks: an undeclared default fails the run once, rather than
    /// producing one invented finding per server.
    /// </summary>
    [Fact]
    public void AnUndeclaredDefaultFailsOnceAndBlocksNothing()
    {
        var mcp = Mcp(McpPolicyDefault.NotDeclared, deny: [McpPolicyRule.Url("http://*")]);

        McpPolicyEvaluator.DescribePolicy(mcp, ".skillforge/policy.yaml")
            .Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpPolicyFailOpen);

        McpPolicyEvaluator
            .Evaluate(mcp, Configuration(Http("unnamed", "https://mcp.elsewhere.com/sse")))
            .Should().BeEmpty();
    }

    [Fact]
    public void AnUndeclaredDefaultStillAppliesTheRulesThatAreThere()
    {
        var mcp = Mcp(McpPolicyDefault.NotDeclared, deny: [McpPolicyRule.Url("http://*")]);

        McpPolicyEvaluator
            .Evaluate(mcp, Configuration(Http("plaintext", "http://mcp.elsewhere.com/sse")))
            .Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpServerBlockedByPolicy);
    }

    [Fact]
    public void ADeclarationThatNamesNeitherACommandNorAUrlIsBlockedByADenyDefault()
    {
        var mcp = Mcp(McpPolicyDefault.Deny, allow: [McpPolicyRule.Url("*")]);

        var unknown = new McpServerDeclaration(
            "mystery",
            "file",
            McpTransport.Unknown,
            null,
            [],
            [],
            "/repo/.mcp.json");

        McpPolicyEvaluator.Evaluate(mcp, Configuration(unknown)).Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpServerBlockedByPolicy);
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var noPolicy = () => McpPolicyEvaluator.Evaluate(null!, Configuration());
        var noConfiguration = () => McpPolicyEvaluator.Evaluate(Mcp(McpPolicyDefault.Deny), null!);
        var noPath = () => McpPolicyEvaluator.DescribePolicy(Mcp(McpPolicyDefault.Deny), " ");

        noPolicy.Should().Throw<ArgumentNullException>();
        noConfiguration.Should().Throw<ArgumentNullException>();
        noPath.Should().Throw<ArgumentException>();
    }

    private static PolicyMcp Mcp(
        McpPolicyDefault @default,
        IReadOnlyList<McpPolicyRule>? allow = null,
        IReadOnlyList<McpPolicyRule>? deny = null) =>
        new([], false, @default, allow ?? [], deny ?? []);

    private static McpConfigurationInspection Configuration(params McpServerDeclaration[] servers) =>
        new("/repo/.mcp.json", servers, [], []);

    private static McpServerDeclaration Http(string name, string url) =>
        new(name, "file", McpTransport.Http, url, [], [], "/repo/.mcp.json");
}
