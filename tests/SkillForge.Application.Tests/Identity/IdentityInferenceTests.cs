using SkillForge.Application.Identity;
using SkillForge.Domain.Identity;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Identity;

/// <summary>
/// What a name is allowed to prove, and what it is not.
/// </summary>
/// <remarks>
/// Every signal here is a name — of an environment variable, of a header — or something a server's own 401 said.
/// The tests that matter most are the last two: a declaration that says nothing produces <c>Unknown</c>, and no
/// credential value ever reaches the model, because the reader never puts one there.
/// </remarks>
public sealed class IdentityInferenceTests
{
    [Fact]
    public void AFederatedTokenFileIsAWorkloadIdentity()
    {
        var identity = Identity(environment: ["AZURE_FEDERATED_TOKEN_FILE"]);

        identity.Identity.Type.Should().Be(AgentIdentityType.WorkloadIdentity);
        identity.Identity.IsLongLived.Should().BeFalse();
    }

    [Fact]
    public void AServiceAccountKeyIsLongLived()
    {
        var identity = Identity(environment: ["GOOGLE_APPLICATION_CREDENTIALS"]);

        identity.Identity.Type.Should().Be(AgentIdentityType.ServiceAccount);
        identity.Identity.IsLongLived.Should().BeTrue();
        identity.Risk.Should().Be(IdentityRisk.Medium);
    }

    [Fact]
    public void AnApiKeyIsAnApiKey()
    {
        Identity(environment: ["GITHUB_API_KEY"]).Identity.Type.Should().Be(AgentIdentityType.ApiKey);
    }

    [Fact]
    public void AnOnBehalfOfNameMakesItDelegated()
    {
        var identity = Identity(environment: ["DEPLOY_ON_BEHALF_OF"]);

        identity.Identity.IsDelegated.Should().BeTrue();
        identity.Risk.Should().Be(IdentityRisk.Medium);
    }

    [Fact]
    public void AnExchangedTokenIsDelegatedWhateverElseTheNamesSay()
    {
        // The exchanged token acts for whoever the subject token belonged to. That is delegation by definition.
        var identity = Identity(environment: ["STS_SUBJECT_TOKEN"]);

        identity.Identity.Type.Should().Be(AgentIdentityType.TokenExchange);
        identity.Identity.IsDelegated.Should().BeTrue();
    }

    [Fact]
    public void TheMostSpecificMachineIdentityWins()
    {
        // Both present. Reporting the API key would name the credential a reviewer least needs to hear about.
        var identity = Identity(environment: ["SERVICE_API_KEY", "AWS_WEB_IDENTITY_TOKEN_FILE"]);

        identity.Identity.Type.Should().Be(AgentIdentityType.WorkloadIdentity);
    }

    [Fact]
    public void AHeaderNameCountsAsEvidenceToo()
    {
        var identity = Identity(headers: ["Authorization"]);

        identity.Evidence.Should().Contain("header Authorization (name only)");
    }

    [Fact]
    public void ADelegatedIdentityWithAWriteScopeIsHigh()
    {
        var probe = McpServerProbe.NeedsAuthorization(
            "deployment-prod",
            new McpAuthorizationChallenge("Bearer", "https://auth.company.com/.well-known/resource", "deployment.write secrets.read"));

        var identity = Identity(environment: ["DEPLOY_ON_BEHALF_OF"], probe: probe);

        identity.Identity.Scopes.Should().Equal("deployment.write", "secrets.read");
        identity.Identity.Issuer.Should().Be("https://auth.company.com");
        identity.Risk.Should().Be(IdentityRisk.High);
    }

    [Fact]
    public void ACredentialInTheChallengeUrlNeverReachesTheIssuer()
    {
        // The metadata URL is a remote server's own text. A server that answers with credentials in it must not
        // have them echoed into a file somebody pastes into a ticket.
        var probe = McpServerProbe.NeedsAuthorization(
            "deployment-prod",
            new McpAuthorizationChallenge("Bearer", "https://user:hunter2@auth.company.com/.well-known/x", null));

        var identity = Identity(probe: probe);

        identity.Identity.Issuer.Should().Be("https://auth.company.com");
    }

    [Fact]
    public void ADeclarationThatSaysNothingIsUnknown()
    {
        var identity = Identity();

        identity.Identity.Type.Should().Be(AgentIdentityType.Unknown);
        identity.Identity.Issuer.Should().BeNull();
        identity.Identity.Scopes.Should().BeEmpty();
        identity.Risk.Should().Be(IdentityRisk.Informational);
    }

    [Fact]
    public void TheSubjectIsNeverFilledIn()
    {
        // A subject lives inside the token, and SkillForge does not read tokens.
        Identity(environment: ["DEPLOY_TOKEN"]).Identity.Subject.Should().BeNull();
    }

    private static McpServerIdentity Identity(
        IReadOnlyList<string>? environment = null,
        IReadOnlyList<string>? headers = null,
        McpServerProbe? probe = null) =>
        IdentityInference.From(
            new McpServerDeclaration(
                "deployment-prod",
                "file",
                McpTransport.Http,
                "https://deploy.company.com/mcp",
                [],
                environment ?? [],
                "/repo/.mcp.json",
                headers),
            probe);
}
