using SkillForge.Domain.Discovery;

namespace SkillForge.Domain.Tests.Discovery;

/// <summary>
/// The rule that decides whether a registry field's value is withheld.
/// </summary>
/// <remarks>
/// Both halves of this matter and pull against each other. Miss a credential name and a token reaches a CI log;
/// match too eagerly and a report redacts a description, which is how people learn to stop reading it. Segment-exact
/// matching is what makes both lists below true at once, and each entry here is a name that appears in real
/// listings.
/// </remarks>
public sealed class SecretFieldNamesTests
{
    [Theory]
    [InlineData("apiKey")]
    [InlineData("api_key")]
    [InlineData("api-key")]
    [InlineData("APIKEY")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("refresh_token")]
    [InlineData("authorization")]
    [InlineData("Authorization")]
    [InlineData("auth")]
    [InlineData("secret")]
    [InlineData("clientSecret")]
    [InlineData("password")]
    [InlineData("passphrase")]
    [InlineData("credential")]
    [InlineData("credentials")]
    [InlineData("sessionCookie")]
    [InlineData("x-vendor-api-key")]
    [InlineData("privateKey")]
    [InlineData("signature")]
    public void WithholdsAValueWhoseNameSaysItIsACredential(string name)
    {
        SecretFieldNames.IsSecretName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("keywords")]
    [InlineData("author")]
    [InlineData("authors")]
    [InlineData("description")]
    [InlineData("name")]
    [InlineData("license")]
    [InlineData("homepage")]
    [InlineData("category")]
    [InlineData("passing")]
    [InlineData("keyboardShortcut")]
    [InlineData("signal")]
    [InlineData("authoredBy")]
    public void KeepsAValueWhoseNameOnlyContainsACredentialWordAsASubstring(string name)
    {
        // A substring rule would withhold every one of these. `keywords` contains "key", `author` contains "auth",
        // `passing` contains "pass", `signal` contains "sig" — and none of them holds a secret.
        SecretFieldNames.IsSecretName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("---")]
    public void SaysNothingAboutANameThatIsNotOne(string? name)
    {
        SecretFieldNames.IsSecretName(name).Should().BeFalse();
    }

    [Fact]
    public void TheWithheldMarkerSaysWhyRatherThanJustMasking()
    {
        // A row of asterisks suggests something is still holding the value. This says the value was never kept.
        SecretFieldNames.Withheld.Should().Contain("withheld").And.Contain("credential");
        SecretFieldNames.Withheld.Should().NotContain("*");
    }
}
