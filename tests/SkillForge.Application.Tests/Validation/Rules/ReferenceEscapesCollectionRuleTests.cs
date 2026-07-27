using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class ReferenceEscapesCollectionRuleTests
{
    private readonly ReferenceEscapesCollectionRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() =>
        _rule.Code.Should().Be(DiagnosticCodes.PathEscapesSkillDirectory);

    [Theory]
    [InlineData("[notes](references/notes.md)")]
    [InlineData("[collapsed](references/../references/notes.md)")]
    [InlineData("[sibling](../other-skill/SKILL.md)")]
    public async Task SaysNothingAboutReferencesInsideTheSkillOrItsNeighbours(string body)
    {
        (await _rule.Run(new SkillBuilder().WithBody(body).Build())).Should().BeEmpty();
    }

    [Theory]
    [InlineData("[secrets](../../.ssh/id_rsa)", "two levels up")]
    [InlineData("[rules](../../rules/react/hooks.md)", "into a config tree")]
    [InlineData("[parent](..)", "the parent directory itself, which cannot be a sibling skill")]
    [InlineData("[deep](../../../elsewhere/file.md)", "three levels up")]
    public async Task ReportsAReferenceThatReachesFurtherThanASibling(string body, string reason)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithBody(body).Build());

        diagnostics.Should().ContainSingle(reason)
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ClimbingAndReturningWithinTheSkillIsNotAnEscape()
    {
        var skill = new SkillBuilder().WithBody("[ok](references/../scripts/run.ps1)").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSameEscapingReferenceTwiceIsReportedOnce()
    {
        var skill = new SkillBuilder()
            .WithBody("[a](../../x/file.md)\n[b](../../x/file.md)")
            .Build();

        (await _rule.Run(skill)).Should().ContainSingle();
    }
}
