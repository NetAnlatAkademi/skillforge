using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class CompatibilityDeclaredRuleTests
{
    private readonly CompatibilityDeclaredRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.CompatibilityMissing);

    [Fact]
    public async Task SaysNothingWhenCompatibilityIsDeclared()
    {
        (await _rule.Run(new SkillBuilder().Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsWhenNoAgentIsListed()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithCompatibility().Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
