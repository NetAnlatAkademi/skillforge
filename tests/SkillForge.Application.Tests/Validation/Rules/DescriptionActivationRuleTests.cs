using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class DescriptionActivationRuleTests
{
    private readonly DescriptionActivationRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() =>
        _rule.Code.Should().Be(DiagnosticCodes.DescriptionWithoutActivationContext);

    [Theory]
    [InlineData("Use this skill when reviewing an ASP.NET Core API before it ships.")]
    [InlineData("Apply while auditing Terraform modules for drift.")]
    [InlineData("Run this during code review of database migrations.")]
    public async Task AcceptsDescriptionsThatSayWhenToApply(string description)
    {
        (await _rule.Run(new SkillBuilder().WithDescription(description).Build())).Should().BeEmpty();
    }

    [Theory]
    [InlineData("A skill for ASP.NET Core APIs and their many interesting qualities.")]
    [InlineData("Reviews code and produces a detailed list of findings for the team.")]
    public async Task WarnsWhenTheDescriptionNeverSaysWhenToApply(string description)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithDescription(description).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task SaysNothingWhenThereIsNoDescriptionAtAll()
    {
        (await _rule.Run(new SkillBuilder().WithDescription(string.Empty).Build())).Should().BeEmpty();
    }
}
