using SkillForge.Application.Modeling;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Evaluation;
using SkillForge.Domain.Modeling;

namespace SkillForge.Application.Tests.Modeling;

/// <summary>
/// The prober turns a model's answers into a rate. What matters is that it counts honestly — a model that chooses the
/// skill 7 times in 10 must be reported as 7 in 10, whatever the threshold says about it.
/// </summary>
public sealed class ActivationProberTests
{
    [Fact]
    public async Task AsksEachPromptAsManyTimesAsTheExpectationSays()
    {
        var runner = new ScriptedRunner("demo-skill");

        var report = await Probe(runner, Expectation(shouldFire: ["do the thing"], runs: 4));

        runner.Requests.Should().Be(4);
        report.RequestCount.Should().Be(4);
    }

    [Fact]
    public async Task CountsTheRunsThatChoseTheSkill()
    {
        // Three of five choose it. The rate is what the report carries; the threshold only decides Met.
        var runner = new ScriptedRunner("demo-skill", "other", "demo-skill", "none", "demo-skill");

        var report = await Probe(runner, Expectation(shouldFire: ["do the thing"], runs: 5, threshold: 0.6));

        var outcome = report.Outcomes.Should().ContainSingle().Subject;
        outcome.ChosenRuns.Should().Be(3);
        outcome.ChosenRate.Should().BeApproximately(0.6, 0.001);
        outcome.Met.Should().BeTrue();
    }

    [Fact]
    public async Task ANegativeCaseAgreesWhenTheSkillIsNotChosen()
    {
        var runner = new ScriptedRunner("none", "none", "other");

        var report = await Probe(runner, Expectation(shouldNotFire: ["translate this"], runs: 3, threshold: 1.0));

        var outcome = report.Outcomes.Should().ContainSingle().Subject;
        outcome.ExpectedToFire.Should().BeFalse();
        outcome.ChosenRuns.Should().Be(0);
        outcome.AgreementRate.Should().Be(1);
        outcome.Met.Should().BeTrue();
    }

    [Fact]
    public async Task ANegativeCaseFailsWhenTheSkillIsChosenTooOften()
    {
        var runner = new ScriptedRunner("demo-skill", "demo-skill", "none");

        var report = await Probe(runner, Expectation(shouldNotFire: ["translate this"], runs: 3, threshold: 0.8));

        report.Outcomes.Should().ContainSingle().Which.Met.Should().BeFalse();
        report.AllMet.Should().BeFalse();
    }

    [Fact]
    public async Task OffersTheDistractorsToTheModelAndRecordsThem()
    {
        // Without competition the probe measures the model's agreeableness rather than the skill's description, so the
        // catalogue it was given has to be visible in the report.
        var runner = new ScriptedRunner("demo-skill");

        var report = await Probe(
            runner,
            Expectation(shouldFire: ["do the thing"], runs: 1),
            [new SkillCandidate("other-skill", "Use this when doing something else entirely.")]);

        report.HadDistractors.Should().BeTrue();
        report.Distractors.Should().Equal("other-skill");
        runner.LastSystemPrompt.Should().Contain("other-skill").And.Contain("demo-skill");
    }

    [Fact]
    public async Task SaysWhenItHadNoDistractors()
    {
        var report = await Probe(new ScriptedRunner("demo-skill"), Expectation(shouldFire: ["x"], runs: 1));

        report.HadDistractors.Should().BeFalse();
    }

    [Theory]
    [InlineData("demo-skill")]
    [InlineData("`demo-skill`")]
    [InlineData("demo-skill.")]
    [InlineData("The answer is demo-skill")]
    [InlineData("DEMO-SKILL")]
    public async Task ReadsTheChoiceThroughTheFormattingModelsAddAnyway(string reply)
    {
        var report = await Probe(new ScriptedRunner(reply), Expectation(shouldFire: ["x"], runs: 1));

        report.Outcomes[0].ChosenRuns.Should().Be(1);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("other-skill")]
    [InlineData("")]
    [InlineData("demo-skillet")]
    public async Task DoesNotReadAChoiceThatIsNotTheSkill(string reply)
    {
        // 'demo-skillet' matters: a substring match would count another skill's name as this one.
        var report = await Probe(new ScriptedRunner(reply), Expectation(shouldFire: ["x"], runs: 1));

        report.Outcomes[0].ChosenRuns.Should().Be(0);
    }

    [Fact]
    public async Task CarriesTheModelIdentityAndWhatItCost()
    {
        var runner = new ScriptedRunner("demo-skill", "demo-skill");

        var report = await Probe(runner, Expectation(shouldFire: ["x"], runs: 2));

        report.Model.Name.Should().Be("test-model");
        report.Model.Endpoint.Should().Be("http://localhost/v1");
        report.PromptTokens.Should().Be(20, "ten per request");
        report.CompletionTokens.Should().Be(4);
    }

    [Fact]
    public async Task RunsPositiveCasesBeforeNegativeOnesSoAReportReadsTheSameEveryTime()
    {
        var runner = new ScriptedRunner("demo-skill", "none");

        var report = await Probe(
            runner,
            Expectation(shouldFire: ["fire for this"], shouldNotFire: ["not for this"], runs: 1));

        report.Outcomes.Select(outcome => outcome.Prompt).Should().Equal("fire for this", "not for this");
    }

    [Fact]
    public void RejectsAMissingRunner()
    {
        var act = () => new ActivationProber(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ModelActivationExpectation Expectation(
        string[]? shouldFire = null,
        string[]? shouldNotFire = null,
        int runs = 1,
        double threshold = 0.8) =>
        new(shouldFire ?? [], shouldNotFire ?? [], runs, threshold);

    private static async Task<ModelActivationReport> Probe(
        ScriptedRunner runner,
        ModelActivationExpectation expectation,
        IReadOnlyList<SkillCandidate>? distractors = null) =>
        await new ActivationProber(runner).ProbeAsync(
            new SkillBuilder().WithName("demo-skill").Build(),
            distractors ?? [],
            expectation,
            CancellationToken.None);

    /// <summary>
    /// Answers from a script, cycling when it runs out, so a test can say "three of five choose it" exactly.
    /// </summary>
    private sealed class ScriptedRunner(params string[] replies) : IModelRunner
    {
        internal int Requests { get; private set; }

        internal string? LastSystemPrompt { get; private set; }

        public ModelIdentity Identity => new("http://localhost/v1", "test-model");

        public Task<ModelCompletion> CompleteAsync(
            ModelPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            LastSystemPrompt = prompt.System;
            var reply = replies[Requests % replies.Length];
            Requests++;

            return Task.FromResult(new ModelCompletion(reply, 10, 2));
        }
    }
}
