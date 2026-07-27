using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class SkillFileLengthRuleTests
{
    private readonly SkillFileLengthRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.SkillFileTooLong);

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task SaysNothingUpToFiveHundredLines(int lineCount)
    {
        (await _rule.Run(new SkillBuilder().WithSkillFileLineCount(lineCount).Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsBeyondFiveHundredLines()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithSkillFileLineCount(642).Build());

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("642");
    }
}
