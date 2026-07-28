using SkillForge.Application.Providers;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Providers;

/// <summary>
/// The checks only ever speak about a provider the skill named, or the caller asked about. Most of these tests
/// exist to pin that down, because the failure mode of a compatibility rule is reporting a portability problem
/// to somebody who never claimed to be portable.
/// </summary>
public sealed class ProviderCompatibilityCheckerTests
{
    private readonly ProviderCompatibilityChecker _checker = new(new AgentProviderRegistry());

    [Fact]
    public void SaysNothingAboutASkillThatFitsTheProviderItDeclares()
    {
        Check(new SkillBuilder().WithCompatibility("claude-code").Build()).Should().BeEmpty();
    }

    [Fact]
    public void SaysNothingWhenNoProviderIsDeclaredAndNoneWasAskedFor()
    {
        // SF1010 already covers "nothing declared". Repeating it here would report one mistake twice.
        Check(new SkillBuilder().WithCompatibility().WithName(TooLongName()).Build()).Should().BeEmpty();
    }

    [Fact]
    public void ReportsAProviderItDoesNotRecognise()
    {
        var diagnostics = Check(new SkillBuilder().WithCompatibility("claude_code").Build());

        var finding = diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.ProviderUnknown);
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
        finding.Message.Should().Contain("claude_code");
        finding.FilePath.Should().Be(SkillDefinition.SkillFileName);
    }

    [Fact]
    public void NamesTheIdentifierANearMissWasMeantToBeAndOffersItAsAFix()
    {
        var finding = Check(new SkillBuilder().WithCompatibility("claude_code").Build()).Single();

        finding.Suggestion.Should().Contain("claude-code");
        finding.Fix.Should().Be("in 'compatibility', replace 'claude_code' with 'claude-code'");
    }

    [Fact]
    public void ListsWhatItKnowsWhenItCannotNameACloseIdentifier()
    {
        // An unrecognised identifier is not necessarily wrong — it may be a provider SkillForge has not learned
        // yet — so the message has to leave that possibility open rather than call it a mistake.
        var finding = Check(new SkillBuilder().WithCompatibility("some-future-agent").Build()).Single();

        finding.Suggestion.Should().Contain("claude-code").And.Contain("github-copilot");
        finding.Fix.Should().BeNull("SkillForge has nothing to offer in place of it");
    }

    [Fact]
    public void SaysWhenTheUnrecognisedProviderCameFromTheCommandLineRatherThanTheSkill()
    {
        var finding = Check(new SkillBuilder().Build(), "typo-agent").Single();

        finding.Message.Should().Contain("--provider");
        finding.Fix.Should().BeNull("the skill's frontmatter is not what needs changing");
    }

    [Fact]
    public void ReportsANameLongerThanTheDeclaredProviderAccepts()
    {
        var skill = new SkillBuilder().WithCompatibility("claude-code").WithName(TooLongName()).Build();

        var finding = Check(skill).Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.ProviderNameTooLong);
        finding.Message.Should().Contain("65").And.Contain("64").And.Contain("Claude Code");
        finding.Suggestion.Should().Contain("docs.claude.com", Exactly.Once());
    }

    [Fact]
    public void ReportsADescriptionLongerThanTheDeclaredProviderAccepts()
    {
        var skill = new SkillBuilder()
            .WithCompatibility("claude-code")
            .WithDescription(new string('d', 1025))
            .Build();

        Check(skill).Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderDescriptionTooLong);
    }

    [Fact]
    public void SaysNothingAboutALimitTheDeclaredProviderDoesNotHave()
    {
        // Codex is recognised but SkillForge has read no limit for it. An unknown limit is not a limit of zero.
        var skill = new SkillBuilder().WithCompatibility("codex").WithName(TooLongName()).Build();

        Check(skill).Should().BeEmpty();
    }

    [Fact]
    public void ChecksAProviderTheCallerAskedAboutEvenThoughTheSkillDoesNotDeclareIt()
    {
        var skill = new SkillBuilder().WithCompatibility("codex").WithName(TooLongName()).Build();

        Check(skill, "claude-code").Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderNameTooLong);
    }

    [Fact]
    public void ChecksAProviderOnceWhenItIsBothDeclaredAndAskedAbout()
    {
        var skill = new SkillBuilder().WithCompatibility("claude-code").WithName(TooLongName()).Build();

        Check(skill, "claude-code").Should().ContainSingle();
    }

    [Fact]
    public void ReportsARepeatedDeclarationOnce()
    {
        Check(new SkillBuilder().WithCompatibility("claude_code", "CLAUDE_CODE").Build())
            .Should().ContainSingle();
    }

    [Fact]
    public void IgnoresABlankEntry()
    {
        // A trailing '-' in the YAML list produces one, and it is not worth a finding of its own.
        Check(new SkillBuilder().WithCompatibility("claude-code", "  ").Build()).Should().BeEmpty();
    }

    [Fact]
    public void KeepsTheSkillsOwnDeclarationsAheadOfTheOnesAskedAbout()
    {
        var skill = new SkillBuilder().WithCompatibility("declared-unknown").Build();

        Check(skill, "asked-unknown").Select(diagnostic => diagnostic.Message)
            .Should().SatisfyRespectively(
                first => first.Should().Contain("declared-unknown"),
                second => second.Should().Contain("asked-unknown"));
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var act = () => new ProviderCompatibilityChecker(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>65 characters — one past what Claude Code documents.</summary>
    private static string TooLongName() => new('a', 65);

    private IReadOnlyList<Diagnostic> Check(SkillDefinition skill, params string[] additionalProviders) =>
        _checker.Check(skill, additionalProviders);
}
