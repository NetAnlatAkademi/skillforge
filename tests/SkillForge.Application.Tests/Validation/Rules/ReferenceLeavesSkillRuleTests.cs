using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class ReferenceLeavesSkillRuleTests
{
    private readonly ReferenceLeavesSkillRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.ReferenceLeavesSkill);

    [Fact]
    public async Task SaysNothingWhenEveryReferenceStaysInside()
    {
        var skill = new SkillBuilder().WithBody("[notes](references/notes.md)").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsAboutASiblingSkillReference()
    {
        // The pattern that made this rule exist: 21 of these on 229 real skills, all deliberate.
        var skill = new SkillBuilder().WithBody("[see](../react-testing/SKILL.md)").Build();

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("react-testing");
        diagnostic.Suggestion.Should().Contain("stand alone");
    }

    [Fact]
    public async Task NamesTheSiblingEvenWhenTheReferenceGoesDeeperIntoIt()
    {
        var skill = new SkillBuilder().WithBody("[deep](../other-skill/references/notes.md)").Build();

        (await _rule.Run(skill)).Should().ContainSingle()
            .Which.Message.Should().Contain("other-skill");
    }

    [Theory]
    [InlineData("[x](../../rules/react/hooks.md)")]
    [InlineData("[x](../../../elsewhere)")]
    public async Task SaysNothingAboutReferencesThatReachFurtherThanASibling(string body)
    {
        // Those belong to SF0008. Reporting them here as well would describe one mistake twice.
        (await _rule.Run(new SkillBuilder().WithBody(body).Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSameSiblingReferenceTwiceIsReportedOnce()
    {
        var skill = new SkillBuilder()
            .WithBody("[a](../other/SKILL.md)\n[b](../other/SKILL.md)")
            .Build();

        (await _rule.Run(skill)).Should().ContainSingle();
    }
}
