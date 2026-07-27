using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class DescriptionRequiredRuleTests
{
    private readonly DescriptionRequiredRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.DescriptionMissing);

    [Fact]
    public async Task SaysNothingWhenTheDescriptionIsPresent()
    {
        (await _rule.Run(new SkillBuilder().Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsAMissingDescription()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithDescription(string.Empty).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }
}
