using SkillForge.Application.Providers;
using SkillForge.Domain.Providers;

namespace SkillForge.Application.Tests.Providers;

/// <summary>
/// The registry decides which <c>compatibility</c> entries are recognised, so a wrong answer here turns a
/// legitimate declaration into a reported typo — or hides a real one.
/// </summary>
public sealed class AgentProviderRegistryTests
{
    private readonly AgentProviderRegistry _registry = new();

    [Fact]
    public void ShipsTheProvidersTheRoadmapNames()
    {
        _registry.Profiles.Select(profile => profile.Id)
            .Should().Equal("claude-code", "codex", "cursor", "github-copilot");
    }

    [Fact]
    public void OrdersProfilesSoOutputIsReproducible()
    {
        _registry.Profiles.Select(profile => profile.Id)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("Claude-Code")]
    [InlineData("  claude-code  ")]
    public void FindsAKnownProviderRegardlessOfCasingOrSurroundingSpace(string id)
    {
        _registry.Find(id)?.Id.Should().Be("claude-code");
    }

    [Fact]
    public void DoesNotFindSomethingItWasNeverTold()
    {
        _registry.Find("some-future-agent").Should().BeNull();
    }

    [Fact]
    public void OnlyClaudeCodeCarriesLimitsToday()
    {
        // Stated as a test rather than left in a comment: the moment another provider's documented limit is read,
        // this test says out loud that the documentation has to be updated with it.
        _registry.Profiles.Where(profile => profile.HasCheckableLimits)
            .Select(profile => profile.Id)
            .Should().Equal("claude-code");
    }

    [Theory]
    [InlineData("claude_code", "claude-code")]
    [InlineData("ClaudeCode", "claude-code")]
    [InlineData("claude-cod", "claude-code")]
    [InlineData("copilot", "github-copilot")]
    [InlineData("cursur", "cursor")]
    public void SuggestsTheIdentifierANearMissWasMeantToBe(string written, string expected)
    {
        _registry.Suggest(written).Should().Be(expected);
    }

    [Theory]
    [InlineData("windsurf")]
    [InlineData("cursor-ide")]
    [InlineData("some-future-agent")]
    [InlineData("")]
    [InlineData("   ")]
    public void SuggestsNothingRatherThanGuessing(string written)
    {
        _registry.Suggest(written).Should().BeNull();
    }

    [Fact]
    public void SuggestsNothingWhenTwoProvidersAreEquallyClose()
    {
        // Naming one of two equally likely candidates would be a coin toss presented as advice.
        var registry = new AgentProviderRegistry(
            new AgentProviderProfile("agent-a", "Agent A", null, null, null),
            new AgentProviderProfile("agent-b", "Agent B", null, null, null));

        registry.Suggest("agent-c").Should().BeNull();
    }
}
