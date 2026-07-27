using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class NameFormatRuleTests
{
    private readonly NameFormatRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.NameInvalid);

    [Theory]
    [InlineData("demo")]
    [InlineData("demo-skill")]
    [InlineData("dotnet-api-review")]
    [InlineData("skill2")]
    [InlineData("a-b-c-d")]
    public async Task AcceptsLowercaseHyphenatedNames(string name)
    {
        (await _rule.Run(new SkillBuilder().WithName(name).Build())).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Demo", "uppercase")]
    [InlineData("demo skill", "a space")]
    [InlineData("demo_skill", "an underscore")]
    [InlineData("-demo", "a leading hyphen")]
    [InlineData("demo-", "a trailing hyphen")]
    [InlineData("demo--skill", "a double hyphen")]
    [InlineData("2demo", "a leading digit")]
    [InlineData("d", "a single character")]
    [InlineData("demo/skill", "a path separator")]
    [InlineData("demo.skill", "a dot")]
    public async Task RejectsNamesThatAreNotUsableIdentifiers(string name, string reason)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithName(name).Build());

        diagnostics.Should().ContainSingle($"'{name}' contains {reason}")
            .Which.Code.Should().Be(DiagnosticCodes.NameInvalid);
    }

    [Fact]
    public async Task SaysNothingWhenThereIsNoNameAtAll()
    {
        // A missing name is SF0004's business. Reporting it twice would be noise.
        (await _rule.Run(new SkillBuilder().WithName(string.Empty).Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task RejectsANameLongerThanSixtyFourCharacters()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithName(new string('a', 65)).Build());

        diagnostics.Should().ContainSingle();
    }
}
