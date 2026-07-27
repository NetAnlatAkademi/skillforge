using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class ReferencePathContainmentRuleTests
{
    private readonly ReferencePathContainmentRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() =>
        _rule.Code.Should().Be(DiagnosticCodes.PathEscapesSkillDirectory);

    [Fact]
    public async Task SaysNothingWhenEveryReferenceStaysInside()
    {
        var skill = new SkillBuilder().WithBody("[notes](references/notes.md)").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("[secrets](../../.ssh/id_rsa)")]
    [InlineData("[parent](../other-skill/SKILL.md)")]
    public async Task ReportsAReferenceThatEscapesTheSkill(string body)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithBody(body).Build());

        diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PathEscapesSkillDirectory);
    }

    [Fact]
    public async Task AcceptsRelativeSegmentsThatStayInside()
    {
        var skill = new SkillBuilder().WithBody("[notes](references/../references/notes.md)").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }
}
