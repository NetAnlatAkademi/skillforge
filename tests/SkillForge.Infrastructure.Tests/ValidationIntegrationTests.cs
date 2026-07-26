using SkillForge.Application.Skills;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// Loads and validates the committed sample skills through the whole chain — real file system, real YAML
/// parser, the default rule set — and pins what each fixture is supposed to say.
/// </summary>
/// <remarks>
/// These are the tests that fail when a rule starts misfiring on realistic input, which unit tests
/// against a hand-built skill cannot show.
/// </remarks>
public sealed class ValidationIntegrationTests
{
    private readonly SkillLoader _loader = new(new FileSystem(), new YamlFrontmatterParser());
    private readonly SkillValidator _validator = new(SkillValidationRules.CreateDefault());

    [Theory]
    [InlineData("valid-skill")]
    [InlineData("dotnet-api-review")]
    public async Task TheGoodSamplesPassEverySingleRule(string sampleName)
    {
        var report = await Validate(sampleName);

        // Named so a failure prints which rule fired rather than just a count.
        report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
            .Should().BeEmpty();
        report.IsValid.Should().BeTrue();
        report.HasFailed(strict: true).Should().BeFalse();
    }

    [Fact]
    public async Task TheBrokenReferencesSampleReportsBothMissingFilesAndNothingElseSevere()
    {
        var report = await Validate("broken-references");

        report.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Message)
            .Should().SatisfyRespectively(
                first => first.Should().Contain("references/checklist.md"),
                second => second.Should().Contain("scripts/analyze.ps1"));

        report.Summary.Errors.Should().Be(2);
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task TheBrokenReferencesSampleStillSeesTheReferenceThatResolves()
    {
        var report = await Validate("broken-references");

        report.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Message.Contains("references/notes.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheBrokenReferencesSampleWarnsAboutUndeclaredCompatibility()
    {
        var report = await Validate("broken-references");

        report.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.CompatibilityMissing);
    }

    [Fact]
    public async Task DiagnosticsAreOrderedErrorsFirst()
    {
        var report = await Validate("broken-references");

        report.Diagnostics.Select(diagnostic => diagnostic.Severity)
            .Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ValidatingTheSameSkillTwiceProducesTheSameReport()
    {
        // Determinism is a contract: CI logs and snapshot tests depend on it.
        var first = await Validate("broken-references");
        var second = await Validate("broken-references");

        first.Diagnostics.Should().Equal(second.Diagnostics);
        first.Summary.Should().Be(second.Summary);
    }

    [Fact]
    public async Task EveryDiagnosticNamesAFileAndOffersASuggestion()
    {
        var report = await Validate("broken-references");

        report.Diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.FilePath.Should().NotBeNullOrWhiteSpace();
            diagnostic.Suggestion.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task AnUnloadableSkillIsReportedWithoutRunningAnyRule()
    {
        var path = RepositoryPaths.Sample("invalid-frontmatter");

        var load = await _loader.LoadAsync(path, CancellationToken.None);
        load.IsSuccess.Should().BeFalse();

        var report = ValidationReport.ForUnloadableSkill(path, load.Diagnostics);

        report.IsValid.Should().BeFalse();
        report.SkillName.Should().BeEmpty();
        report.Summary.Errors.Should().Be(1);
        report.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.FrontmatterNotParsable);
    }

    private async Task<ValidationReport> Validate(string sampleName)
    {
        var load = await _loader.LoadAsync(RepositoryPaths.Sample(sampleName), CancellationToken.None);
        load.IsSuccess.Should().BeTrue($"the '{sampleName}' sample must load");

        return await _validator.ValidateAsync(load.Value!, CancellationToken.None);
    }
}
