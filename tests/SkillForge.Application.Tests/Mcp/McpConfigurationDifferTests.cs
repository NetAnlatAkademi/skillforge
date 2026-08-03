using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// What an MCP diff claims, and the two things it refuses to: an environment variable's value, and that a
/// narrowing is growth.
/// </summary>
public sealed class McpConfigurationDifferTests
{
    [Fact]
    public void IdenticalConfigurationsHaveNoChanges()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog")),
            Inspection(Server("catalog")));

        diff.HasChanges.Should().BeFalse();
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ANewServerIsGrowth()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog")),
            Inspection(Server("catalog"), Server("legacy")));

        diff.ServersAdded.Should().Equal("legacy");
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ARemovedServerIsAChangeButNotGrowth()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog"), Server("legacy")),
            Inspection(Server("catalog")));

        diff.ServersRemoved.Should().Equal("legacy");
        diff.HasChanges.Should().BeTrue();
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ARepointedServerIsGrowthBecauseTheRequestNowLeavesForSomewhereElse()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog", command: "https://catalog.internal.example/mcp")),
            Inspection(Server("catalog", command: "https://catalog.other.example/mcp")));

        diff.Changed.Should().ContainSingle()
            .Which.Command!.After.Should().Be("https://catalog.other.example/mcp");
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ANewEnvironmentVariableIsReportedByNameAndIsNotGrowth()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("obsidian", environment: ["OBSIDIAN_TOKEN"])),
            Inspection(Server("obsidian", environment: ["OBSIDIAN_TOKEN", "OBSIDIAN_VAULT"])));

        diff.Changed.Should().ContainSingle()
            .Which.EnvironmentVariableNames.Added.Should().Equal("OBSIDIAN_VAULT");
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void AChangedArgumentIsReportedAsOneAddedAndOneRemoved()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("obsidian", arguments: ["-y", "obsidian-mcp"])),
            Inspection(Server("obsidian", arguments: ["-y", "obsidian-mcp@1.4.2"])));

        var change = diff.Changed.Should().ContainSingle().Subject;
        change.Arguments.Added.Should().Equal("obsidian-mcp@1.4.2");
        change.Arguments.Removed.Should().Equal("obsidian-mcp");
    }

    [Fact]
    public void ATransportChangeIsReportedAndIsGrowth()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog", transport: McpTransport.Stdio)),
            Inspection(Server("catalog", transport: McpTransport.Http)));

        diff.Changed.Should().ContainSingle().Which.Transport!.After.Should().Be("Http");
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ARenamedServerReadsAsOneRemovedAndOneAdded()
    {
        // Honest rather than clever: a consumer referring to the old name no longer has it.
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("catalog")),
            Inspection(Server("service-catalog")));

        diff.ServersAdded.Should().Equal("service-catalog");
        diff.ServersRemoved.Should().Equal("catalog");
        diff.Changed.Should().BeEmpty();
    }

    [Fact]
    public void ServerListsAreOrderedSoARunOverUnchangedInputReadsTheSame()
    {
        var diff = McpConfigurationDiffer.Compare(
            Inspection(Server("a")),
            Inspection(Server("a"), Server("z"), Server("m")));

        diff.ServersAdded.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAMissingSide()
    {
        var noBefore = () => McpConfigurationDiffer.Compare(null!, Inspection());
        var noAfter = () => McpConfigurationDiffer.Compare(Inspection(), null!);

        noBefore.Should().Throw<ArgumentNullException>();
        noAfter.Should().Throw<ArgumentNullException>();
    }

    private static McpConfigurationInspection Inspection(params McpServerDeclaration[] servers) =>
        new("mcp.json", servers, [], []);

    private static McpServerDeclaration Server(
        string name,
        McpTransport transport = McpTransport.Http,
        string? command = "https://example.com/mcp",
        IReadOnlyList<string>? arguments = null,
        IReadOnlyList<string>? environment = null) =>
        new(name, "file", transport, command, arguments ?? [], environment ?? [], "mcp.json");
}
