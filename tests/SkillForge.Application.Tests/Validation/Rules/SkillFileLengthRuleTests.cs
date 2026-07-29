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
    [InlineData(734)]
    [InlineData(1000)]
    public async Task SaysNothingUpToAThousandLines(int lineCount)
    {
        // 734 is the longest SKILL.md in the 229-skill corpus. Pinned so a future tightening of the threshold has to
        // face the fact that it would start speaking about a real skill again.
        (await _rule.Run(new SkillBuilder().WithSkillFileLineCount(lineCount).Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsBeyondAThousandLines()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithSkillFileLineCount(1042).Build());

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("1042").And.Contain("1000");
    }
}
