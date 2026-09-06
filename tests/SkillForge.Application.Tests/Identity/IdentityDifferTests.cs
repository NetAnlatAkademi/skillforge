using SkillForge.Application.Identity;
using SkillForge.Domain.Identity;

namespace SkillForge.Application.Tests.Identity;

/// <summary>
/// Which identity changes are worth a finding, and which are worth only a line in the report.
/// </summary>
/// <remarks>
/// Widenings only, in both directions of the asymmetry: a scope added is coded, a scope dropped is not, and a
/// long-lived credential replaced by a short-lived one is a improvement nobody needs warning about.
/// </remarks>
public sealed class IdentityDifferTests
{
    [Fact]
    public void AChangedIdentityTypeIsSF7101()
    {
        var findings = Findings(
            Server(AgentIdentityType.UserOAuth),
            Server(AgentIdentityType.WorkloadIdentity));

        findings.Should().ContainSingle().Which.Code.Should().Be("SF7101");
    }

    [Fact]
    public void DelegationBeingTurnedOnIsSF7202()
    {
        var findings = Findings(
            Server(AgentIdentityType.ServiceAccount),
            Server(AgentIdentityType.ServiceAccount, delegated: true));

        findings.Should().Contain(finding => finding.Code == "SF7202");
    }

    [Fact]
    public void ANewScopeIsSF7201()
    {
        var findings = Findings(
            Server(AgentIdentityType.UserOAuth, scopes: ["deployment.read"]),
            Server(AgentIdentityType.UserOAuth, scopes: ["deployment.read", "secrets.read"]));

        var finding = findings.Should().ContainSingle().Subject;
        finding.Code.Should().Be("SF7201");
        finding.Message.Should().Contain("secrets.read");
    }

    [Fact]
    public void ALongerLivedCredentialIsSF7102()
    {
        var findings = Findings(
            Server(AgentIdentityType.UserOAuth),
            Server(AgentIdentityType.UserOAuth, longLived: true));

        findings.Should().Contain(finding => finding.Code == "SF7102");
    }

    [Fact]
    public void ADroppedScopeIsShownAndNotCoded()
    {
        var before = Server(AgentIdentityType.UserOAuth, scopes: ["deployment.read", "secrets.read"]);
        var after = Server(AgentIdentityType.UserOAuth, scopes: ["deployment.read"]);

        var diff = IdentityDiffer.Compare(Report("/before", before), Report("/after", after));

        diff.Changed.Should().ContainSingle().Which.Scopes.Removed.Should().Equal("secrets.read");
        IdentityDiffer.Findings(diff).Should().BeEmpty();
    }

    [Fact]
    public void AShorterLivedCredentialIsNotAFinding()
    {
        Findings(
            Server(AgentIdentityType.ApiKey, longLived: true),
            Server(AgentIdentityType.ApiKey)).Should().NotContain(finding => finding.Code == "SF7102");
    }

    [Fact]
    public void AnUnchangedIdentityProducesNothing()
    {
        var diff = IdentityDiffer.Compare(
            Report("/before", Server(AgentIdentityType.UserOAuth)),
            Report("/after", Server(AgentIdentityType.UserOAuth)));

        diff.HasChanges.Should().BeFalse();
        IdentityDiffer.Findings(diff).Should().BeEmpty();
    }

    [Fact]
    public void ARenamedServerIsOneGoneAndOneArrived()
    {
        // Guessing that two differently named entries are the same server would be an invention, and the wrong
        // one hides an identity change behind a rename.
        var diff = IdentityDiffer.Compare(
            Report("/before", Server(AgentIdentityType.UserOAuth, name: "old-name")),
            Report("/after", Server(AgentIdentityType.WorkloadIdentity, name: "new-name")));

        diff.Added.Should().ContainSingle().Which.ServerName.Should().Be("new-name");
        diff.Removed.Should().ContainSingle().Which.ServerName.Should().Be("old-name");
        diff.Changed.Should().BeEmpty();
    }

    private static IReadOnlyList<Domain.Diagnostics.Diagnostic> Findings(
        McpServerIdentity before,
        McpServerIdentity after) =>
        IdentityDiffer.Findings(IdentityDiffer.Compare(Report("/before", before), Report("/after", after)));

    private static McpIdentityReport Report(string path, McpServerIdentity server) => new(path, [server], []);

    private static McpServerIdentity Server(
        AgentIdentityType type,
        bool delegated = false,
        bool longLived = false,
        IReadOnlyList<string>? scopes = null,
        string name = "deployment-prod") =>
        new(
            name,
            "/repo/.mcp.json",
            new AgentIdentity(type, null, null, scopes ?? [], delegated, longLived),
            IdentityRisk.Informational,
            "test",
            []);
}
