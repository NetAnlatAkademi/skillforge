using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation;

public sealed class SkillValidatorTests
{
    [Fact]
    public async Task CollectsFindingsFromEveryRule()
    {
        var validator = new SkillValidator(
        [
            StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning),
            StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error),
        ]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().BeEquivalentTo([DiagnosticCodes.NameMissing, DiagnosticCodes.LicenseMissing]);
    }

    [Fact]
    public async Task AnErrorFromOneRuleDoesNotStopTheOthers()
    {
        // The user should see everything wrong with a skill in one run.
        var later = StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning);
        var validator = new SkillValidator(
        [
            StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error),
            later,
        ]);

        await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        later.WasRun.Should().BeTrue();
    }

    [Fact]
    public async Task ReportsAnEmptyResultForASkillWithNothingWrong()
    {
        var validator = new SkillValidator([StubRule.Silent()]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.Diagnostics.Should().BeEmpty();
        report.IsValid.Should().BeTrue();
        report.Summary.Total.Should().Be(0);
    }

    [Fact]
    public async Task SummarisesBySeverity()
    {
        var validator = new SkillValidator(
        [
            StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error),
            StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning),
            StubRule.Reporting(DiagnosticCodes.CompatibilityMissing, DiagnosticSeverity.Warning),
            StubRule.Reporting(DiagnosticCodes.ContainsScript, DiagnosticSeverity.Info),
        ]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.Summary.Errors.Should().Be(1);
        report.Summary.Warnings.Should().Be(2);
        report.Summary.Info.Should().Be(1);
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task WarningsAloneLeaveTheSkillValid()
    {
        var validator = new SkillValidator(
            [StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning)]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.IsValid.Should().BeTrue();
        report.HasFailed(strict: false).Should().BeFalse();
        report.HasFailed(strict: true).Should().BeTrue();
    }

    [Fact]
    public async Task ErrorsFailInBothModes()
    {
        var validator = new SkillValidator(
            [StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error)]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.HasFailed(strict: false).Should().BeTrue();
        report.HasFailed(strict: true).Should().BeTrue();
    }

    [Fact]
    public async Task InfoNeverFails()
    {
        var validator = new SkillValidator(
            [StubRule.Reporting(DiagnosticCodes.ContainsScript, DiagnosticSeverity.Info)]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.HasFailed(strict: false).Should().BeFalse();
        report.HasFailed(strict: true).Should().BeFalse();
    }

    [Fact]
    public async Task OutputIsIndependentOfTheOrderRulesAreGivenIn()
    {
        var forwards = new SkillValidator(
        [
            StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning),
            StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error),
            StubRule.Reporting(DiagnosticCodes.CompatibilityMissing, DiagnosticSeverity.Warning),
        ]);

        var backwards = new SkillValidator(
        [
            StubRule.Reporting(DiagnosticCodes.CompatibilityMissing, DiagnosticSeverity.Warning),
            StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error),
            StubRule.Reporting(DiagnosticCodes.LicenseMissing, DiagnosticSeverity.Warning),
        ]);

        var skill = new SkillBuilder().Build();
        var first = await forwards.ValidateAsync(skill, CancellationToken.None);
        var second = await backwards.ValidateAsync(skill, CancellationToken.None);

        first.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().Equal(second.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public async Task ReportNamesTheSkillAndItsPath()
    {
        var validator = new SkillValidator([StubRule.Silent()]);

        var report = await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        report.SkillName.Should().Be("demo-skill");
        report.SkillPath.Should().Be("/skills/demo");
    }

    [Fact]
    public async Task ARuleThatThrowsIsABugAndIsNotSwallowed()
    {
        // Exit code 3 territory: a broken rule must be loud, not silently skipped.
        var validator = new SkillValidator([StubRule.Throwing()]);

        var act = async () =>
            await validator.ValidateAsync(new SkillBuilder().Build(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CancellationIsPassedToTheRules()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var validator = new SkillValidator([StubRule.Silent()]);

        var act = async () =>
            await validator.ValidateAsync(new SkillBuilder().Build(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ExposesTheCodesItWillRun()
    {
        var validator = new SkillValidator(
            [StubRule.Reporting(DiagnosticCodes.NameMissing, DiagnosticSeverity.Error)]);

        validator.RuleCodes.Should().Equal(DiagnosticCodes.NameMissing);
    }

    [Fact]
    public void RejectsAMissingRuleCollection()
    {
        var act = () => new SkillValidator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TheDefaultRuleSetCoversTheImplementedRulesExactlyOnce()
    {
        var codes = SkillValidationRules.CreateDefault(new Fakes.FakeFileSystem()).Select(rule => rule.Code).ToArray();

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().Contain(
        [
            DiagnosticCodes.NameMissing,
            DiagnosticCodes.DescriptionMissing,
            DiagnosticCodes.NameInvalid,
            DiagnosticCodes.ReferencedFileNotFound,
            DiagnosticCodes.PathEscapesSkillDirectory,
            DiagnosticCodes.PackageVersionInvalid,
            DiagnosticCodes.DescriptionTooShort,
            DiagnosticCodes.DescriptionWithoutActivationContext,
            DiagnosticCodes.SkillFileTooLong,
            DiagnosticCodes.LicenseMissing,
            DiagnosticCodes.CompatibilityMissing,
            DiagnosticCodes.ExternalUrlPresent,
            DiagnosticCodes.ScriptWithoutDeclaredPermission,
            DiagnosticCodes.BroadShellPrivileges,
            DiagnosticCodes.ReferenceLeavesSkill,
        ]);
    }

    private sealed class StubRule : ISkillValidationRule
    {
        private readonly Diagnostic? _diagnostic;
        private readonly bool _throws;

        private StubRule(string code, Diagnostic? diagnostic, bool throws = false)
        {
            Code = code;
            _diagnostic = diagnostic;
            _throws = throws;
        }

        public string Code { get; }

        internal bool WasRun { get; private set; }

        internal static StubRule Reporting(string code, DiagnosticSeverity severity) =>
            new(code, new Diagnostic(code, severity, $"stub finding for {code}", "SKILL.md", 1));

        internal static StubRule Silent() => new(DiagnosticCodes.ContainsScript, diagnostic: null);

        internal static StubRule Throwing() =>
            new(DiagnosticCodes.ContainsScript, diagnostic: null, throws: true);

        public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
            SkillDefinition skill,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasRun = true;

            if (_throws)
            {
                throw new InvalidOperationException("this rule is broken");
            }

            IReadOnlyList<Diagnostic> result = _diagnostic is null ? [] : [_diagnostic];
            return ValueTask.FromResult(result);
        }
    }
}
