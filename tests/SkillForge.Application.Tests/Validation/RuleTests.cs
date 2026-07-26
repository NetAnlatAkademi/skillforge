using SkillForge.Application.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation;

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

public sealed class ReferencedFileExistsRuleTests
{
    private readonly ReferencedFileExistsRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.ReferencedFileNotFound);

    [Fact]
    public async Task SaysNothingWhenEveryReferenceResolves()
    {
        var skill = new SkillBuilder()
            .WithBody("See [notes](references/notes.md).")
            .WithResources("SKILL.md", "references/notes.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsAReferenceThatDoesNotResolve()
    {
        var skill = new SkillBuilder()
            .WithBody("See [checklist](references/checklist.md).", bodyStartLine: 10)
            .WithResources("SKILL.md")
            .Build();

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ReferencedFileNotFound);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Line.Should().Be(10);
        diagnostic.Message.Should().Contain("references/checklist.md");
    }

    [Fact]
    public async Task ReportsEveryBrokenReferenceOnce()
    {
        var skill = new SkillBuilder()
            .WithBody("[a](references/a.md)\n[b](scripts/b.ps1)\n[a again](references/a.md)")
            .WithResources("SKILL.md")
            .Build();

        var diagnostics = await _rule.Run(skill);

        // The same missing file mentioned twice is one problem, reported once, at its first mention.
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.Line).Should().Equal(6, 7);
    }

    [Fact]
    public async Task IgnoresExternalAndAnchorLinks()
    {
        var skill = new SkillBuilder()
            .WithBody("""
                [docs](https://learn.microsoft.com/)
                [insecure](http://example.com/page)
                [mail](mailto:someone@example.com)
                [section](#workflow)
                """)
            .WithResources("SKILL.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task IgnoresLinksInsideFencedCodeBlocks()
    {
        var skill = new SkillBuilder()
            .WithBody("""
                Example of what a reference looks like:

                ```markdown
                [notes](references/does-not-exist.md)
                ```
                """)
            .WithResources("SKILL.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task MatchesReferencesWrittenWithALeadingDotSlash()
    {
        var skill = new SkillBuilder()
            .WithBody("See [notes](./references/notes.md).")
            .WithResources("SKILL.md", "references/notes.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }
}

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

public sealed class PackageVersionRuleTests
{
    private readonly PackageVersionRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.PackageVersionInvalid);

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.1.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0+build.5")]
    public async Task AcceptsSemanticVersions(string version)
    {
        (await _rule.Run(new SkillBuilder().WithVersion(version).Build())).Should().BeEmpty();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("latest")]
    [InlineData("1.0.0.0")]
    public async Task RejectsAnythingElse(string version)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithVersion(version).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SaysNothingWhenNoVersionIsDeclared()
    {
        // A version is optional in the first release; only a malformed one is an error.
        (await _rule.Run(new SkillBuilder().WithVersion(null).Build())).Should().BeEmpty();
    }
}

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

public sealed class LicenseDeclaredRuleTests
{
    private readonly LicenseDeclaredRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.LicenseMissing);

    [Fact]
    public async Task SaysNothingWhenALicenseIsDeclared()
    {
        (await _rule.Run(new SkillBuilder().Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsWhenNoLicenseIsDeclared()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithLicense(null).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}

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

/// <summary>Runs a rule with the ceremony a test does not need to spell out.</summary>
internal static class RuleTestExtensions
{
    internal static async Task<IReadOnlyList<Diagnostic>> Run(
        this ISkillValidationRule rule,
        SkillDefinition skill) =>
        await rule.ValidateAsync(skill, CancellationToken.None);
}
