using SkillForge.Application.Policy;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Tests.Policy;

/// <summary>
/// Every MCP policy decision rests on this class, so the cases that would let something through are the ones worth
/// pinning: a URL that is the same endpoint written differently, a wildcard that reaches further than it reads, and
/// a rule matching a transport it is not about.
/// </summary>
public sealed class McpPolicyRuleMatcherTests
{
    [Fact]
    public void AUrlRuleMatchesTheSameEndpointWrittenDifferently()
    {
        var rule = McpPolicyRule.Url("https://mcp.company.com/github");

        McpPolicyRuleMatcher.Matches(rule, Http("HTTPS://MCP.Company.com:443/github/")).Should().BeTrue();
    }

    [Fact]
    public void AUrlWildcardMatchesEverythingBelowIt()
    {
        var rule = McpPolicyRule.Url("https://mcp.company.com/*");

        McpPolicyRuleMatcher.Matches(rule, Http("https://mcp.company.com/github")).Should().BeTrue();
        McpPolicyRuleMatcher.Matches(rule, Http("https://mcp.elsewhere.com/github")).Should().BeFalse();
    }

    /// <summary>
    /// A dot is a dot. If the pattern were compiled as a regular expression without escaping, this would match.
    /// </summary>
    [Fact]
    public void EverythingExceptTheStarIsLiteral()
    {
        var rule = McpPolicyRule.Url("https://mcp.company.com");

        McpPolicyRuleMatcher.Matches(rule, Http("https://mcpXcompany.com")).Should().BeFalse();
    }

    [Fact]
    public void AUrlRuleNeverMatchesALocalCommand()
    {
        var rule = McpPolicyRule.Url("*");

        McpPolicyRuleMatcher.Matches(rule, Stdio("npx", "@company/mcp")).Should().BeFalse();
    }

    [Fact]
    public void ACommandRuleNeverMatchesAUrl()
    {
        var rule = McpPolicyRule.Command("*");

        McpPolicyRuleMatcher.Matches(rule, Http("https://mcp.company.com")).Should().BeFalse();
    }

    [Fact]
    public void ACommandRuleRequiresEveryArgumentItNamesButNotInOrder()
    {
        var rule = McpPolicyRule.Command("npx", "@company/internal-mcp");

        McpPolicyRuleMatcher.Matches(rule, Stdio("npx", "-y", "@company/internal-mcp")).Should().BeTrue();
        McpPolicyRuleMatcher.Matches(rule, Stdio("npx", "-y", "@other/mcp")).Should().BeFalse();
    }

    [Fact]
    public void ANameRuleMatchesWhateverTheConfigurationCalledIt()
    {
        McpPolicyRuleMatcher.Matches(McpPolicyRule.Name("internal-*"), Http("https://x", "internal-notes"))
            .Should().BeTrue();
    }

    [Fact]
    public void AWiderPatternGeneralisesTheNarrowerOne()
    {
        McpPolicyRuleMatcher
            .Generalises(McpPolicyRule.Url("*.company.com"), McpPolicyRule.Url("api.company.com"))
            .Should().BeTrue();
    }

    [Fact]
    public void ANarrowerPatternDoesNotGeneraliseTheWiderOne()
    {
        McpPolicyRuleMatcher
            .Generalises(McpPolicyRule.Url("api.company.com"), McpPolicyRule.Url("*.company.com"))
            .Should().BeFalse();
    }

    [Fact]
    public void ARuleDoesNotGeneraliseItself()
    {
        var rule = McpPolicyRule.Url("*.company.com");

        McpPolicyRuleMatcher.Generalises(rule, rule).Should().BeFalse();
    }

    [Fact]
    public void RulesOfDifferentKindsAreNeverComparable()
    {
        McpPolicyRuleMatcher
            .Generalises(McpPolicyRule.Name("*"), McpPolicyRule.Url("api.company.com"))
            .Should().BeFalse();
    }

    /// <summary>
    /// Dropping fewer arguments is a widening: <c>npx</c> alone permits everything <c>npx @company/mcp</c> did.
    /// </summary>
    [Fact]
    public void ACommandWithoutArgumentsGeneralisesTheSameCommandWithThem()
    {
        McpPolicyRuleMatcher
            .Generalises(McpPolicyRule.Command("npx"), McpPolicyRule.Command("npx", "@company/mcp"))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("https://mcp.company.com/", "https://mcp.company.com")]
    [InlineData("HTTP://Example.COM:80/a", "http://example.com/a")]
    [InlineData("https://example.com:8443/a", "https://example.com:8443/a")]
    [InlineData("http://*", "http://*")]
    [InlineData("not-a-url", "not-a-url")]
    public void CanonicalisationKeepsWhatDistinguishesTwoEndpoints(string input, string expected)
    {
        McpPolicyRuleMatcher.CanonicaliseUrl(input).Should().Be(expected);
    }

    [Fact]
    public void RejectsAMissingRuleOrServer()
    {
        var noRule = () => McpPolicyRuleMatcher.Matches(null!, Http("https://x"));
        var noServer = () => McpPolicyRuleMatcher.Matches(McpPolicyRule.Url("*"), null!);

        noRule.Should().Throw<ArgumentNullException>();
        noServer.Should().Throw<ArgumentNullException>();
    }

    private static McpServerDeclaration Http(string url, string name = "server") =>
        new(name, "file", McpTransport.Http, url, [], [], "/repo/.mcp.json");

    private static McpServerDeclaration Stdio(string command, params string[] arguments) =>
        new("server", "file", McpTransport.Stdio, command, arguments, [], "/repo/.mcp.json");
}
