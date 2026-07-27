using SkillForge.Application.Evaluation;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Evaluation;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Evaluation;

public sealed class EvalRunnerTests
{
    [Fact]
    public void ARequiredFileThatIsPresentPasses()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "references/checklist.md").Build();

        var result = Run(skill, Case(files: ["references/checklist.md"]));

        result.Passed.Should().BeTrue();
        result.Cases.Should().ContainSingle().Which.Passed.Should().BeTrue();
    }

    [Fact]
    public void ARequiredFileThatIsMissingFailsAndSaysSo()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md").Build();

        var result = Run(skill, Case(files: ["references/checklist.md"]));

        result.Passed.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.Cases[0].Failures.Should().ContainSingle()
            .Which.Detail.Should().Be("the skill does not contain it");
    }

    [Fact]
    public void AForbiddenDiagnosticNamesWhereItWasFound()
    {
        // "SF0007 came back" is not enough to act on; the location is the point.
        var report = Report(Diagnostic.Error(
            DiagnosticCodes.ReferencedFileNotFound, "Missing.", "SKILL.md", 16));

        var result = Run(new SkillBuilder().Build(), Case(forbid: ["SF0007"]), report);

        result.Passed.Should().BeFalse();
        result.Cases[0].Failures.Should().ContainSingle().Which.Detail.Should().Be("SKILL.md:16");
    }

    [Fact]
    public void AnExpectedDiagnosticLetsASkillPinAFindingItHasAccepted()
    {
        // Without this, an author who has decided SF1009 does not apply must either fix it or keep their evals red.
        var report = Report(Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.", "SKILL.md", 1));

        Run(new SkillBuilder().Build(), Case(expect: ["SF1009"]), report).Passed.Should().BeTrue();
        Run(new SkillBuilder().Build(), Case(expect: ["SF1009"])).Passed.Should().BeFalse();
    }

    [Fact]
    public void ADescriptionTermIsMatchedWithoutCaringAboutCase()
    {
        var skill = new SkillBuilder().WithDescription("Use this skill when reviewing an ASP.NET Core API.").Build();

        Run(skill, Case(mentions: ["asp.net core"])).Passed.Should().BeTrue();
    }

    [Fact]
    public void VocabularyOverlapPassesWhenThePromptSharesWords()
    {
        var skill = new SkillBuilder()
            .WithDescription("Use this skill when reviewing an ASP.NET Core API before it ships.")
            .Build();

        var result = Run(skill, Case(activation: new ActivationExpectation(
            "review my ASP.NET Core API for security problems",
            ExpectOverlap: true)));

        result.Passed.Should().BeTrue();
        result.Cases[0].Assertions[0].Detail.Should().Contain("shared:");
    }

    [Fact]
    public void VocabularyOverlapFailsWhenThereIsNoneAndTheClaimIsAboutWordingOnly()
    {
        var skill = new SkillBuilder().WithDescription("Use this skill when tuning a database index.").Build();

        var result = Run(skill, Case(activation: new ActivationExpectation(
            "translate this paragraph into Turkish",
            ExpectOverlap: true)));

        result.Passed.Should().BeFalse();

        // The wording matters as much as the verdict. This must never claim the skill "would not fire" -- SkillForge
        // does not run a model and cannot know that.
        var assertion = result.Cases[0].Assertions[0];
        assertion.Description.Should().Contain("shares wording");
        assertion.Description.Should().NotContain("activat");
        assertion.Description.Should().NotContain("fire");
    }

    [Fact]
    public void ANegativeVocabularyCasePassesWhenNothingIsShared()
    {
        var skill = new SkillBuilder().WithDescription("Use this skill when tuning a database index.").Build();

        Run(skill, Case(activation: new ActivationExpectation(
            "translate this paragraph into Turkish",
            ExpectOverlap: false))).Passed.Should().BeTrue();
    }

    [Fact]
    public void ShortWordsDoNotCountAsSharedVocabulary()
    {
        // "the", "my", "for", "an" are in every sentence ever written. Counting them would make every case pass.
        var skill = new SkillBuilder().WithDescription("Use the tool for an index.").Build();

        Run(skill, Case(activation: new ActivationExpectation(
            "the for my an use",
            ExpectOverlap: false))).Passed.Should().BeTrue();
    }

    [Fact]
    public void ACaseThatAssertsNothingIsSkippedNotPassed()
    {
        var result = Run(new SkillBuilder().Build(), EvalCase.Empty("does nothing"));

        result.SkippedCount.Should().Be(1);
        result.PassedCount.Should().Be(0);
        result.Cases[0].Skipped.Should().BeTrue();
    }

    [Fact]
    public void ASuiteWithNoCasesIsNotAPass()
    {
        // An empty evals folder means nobody has written any evals. Reporting green would say the opposite.
        var report = EvalRunner.Run(new SkillBuilder().Build(), Report(), []);

        report.Passed.Should().BeFalse();
        report.Cases.Should().BeEmpty();
    }

    [Fact]
    public void ShellPermissionCanBeRequiredOrForbidden()
    {
        var withShell = new SkillBuilder().Build() with
        {
            Configuration = SkillConfiguration.Default with { Exists = true, ShellAllowed = ["bash"] },
        };
        var withoutShell = new SkillBuilder().Build();

        Run(withShell, Case(shell: true)).Passed.Should().BeTrue();
        Run(withoutShell, Case(shell: true)).Passed.Should().BeFalse();
        Run(withoutShell, Case(shell: false)).Passed.Should().BeTrue();
        Run(withShell, Case(shell: false)).Passed.Should().BeFalse();
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var noSkill = () => EvalRunner.Run(null!, Report(), []);
        var noReport = () => EvalRunner.Run(new SkillBuilder().Build(), null!, []);
        var noCases = () => EvalRunner.Run(new SkillBuilder().Build(), Report(), null!);

        noSkill.Should().Throw<ArgumentNullException>();
        noReport.Should().Throw<ArgumentNullException>();
        noCases.Should().Throw<ArgumentNullException>();
    }

    private static EvalReport Run(
        Domain.Skills.SkillDefinition skill,
        EvalCase evalCase,
        ValidationReport? report = null) =>
        EvalRunner.Run(skill, report ?? Report(), [evalCase]);

    private static EvalCase Case(
        IReadOnlyList<string>? files = null,
        bool? shell = null,
        IReadOnlyList<string>? forbid = null,
        IReadOnlyList<string>? expect = null,
        IReadOnlyList<string>? mentions = null,
        ActivationExpectation? activation = null) =>
        new("a case", files ?? [], shell, forbid ?? [], expect ?? [], mentions ?? [], activation);

    private static ValidationReport Report(params Diagnostic[] diagnostics) =>
        new("demo-skill", "/skills/demo", diagnostics, ValidationSummary.FromDiagnostics(diagnostics));
}
