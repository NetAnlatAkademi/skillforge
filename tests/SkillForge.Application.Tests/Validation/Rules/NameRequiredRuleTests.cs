using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation.Rules;

/// <summary>
/// One class per rule. Each rule is exercised on its own, against a skill that would otherwise pass
/// everything, so a failure names exactly one cause.
/// </summary>
public sealed class NameRequiredRuleTests
{
    private readonly NameRequiredRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.NameMissing);

    [Fact]
    public async Task SaysNothingWhenTheNameIsPresent()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().Build());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsAMissingName()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithName(string.Empty).Build());

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.NameMissing);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.FilePath.Should().Be(SkillDefinition.SkillFileName);
        diagnostic.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReportsAWhitespaceOnlyName()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithName("   ").Build());

        diagnostics.Should().ContainSingle();
    }
}
