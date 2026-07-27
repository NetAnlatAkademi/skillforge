using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class DescriptionLengthRuleTests
{
    private readonly DescriptionLengthRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.DescriptionTooShort);

    [Fact]
    public async Task SaysNothingAboutAUsefulDescription()
    {
        (await _rule.Run(new SkillBuilder().Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsAboutAShortDescription()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithDescription("Reviews APIs.").Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task SaysNothingWhenThereIsNoDescriptionAtAll()
    {
        // A missing description is SF0005's business.
        (await _rule.Run(new SkillBuilder().WithDescription(string.Empty).Build())).Should().BeEmpty();
    }
}
