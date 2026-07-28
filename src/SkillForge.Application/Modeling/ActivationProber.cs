using SkillForge.Domain.Evaluation;
using SkillForge.Domain.Modeling;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Modeling;

/// <summary>
/// Asks a model whether it would choose a skill for a prompt, several times, and reports the rate.
/// </summary>
/// <remarks>
/// This is the thing the deterministic vocabulary check was careful not to claim to be. It is still not proof: a model
/// answering a question about a list of skills is not the same as an agent mid-conversation with a toolbox and a task.
/// It is a much closer approximation than word overlap, and the report says which model produced it so a reader can
/// weigh it.
///
/// Two design choices carry most of the honesty here:
///
/// **Distractors.** The skill is offered alongside its siblings. Asked on its own, a model says yes to almost anything,
/// so a probe without competition measures the model's agreeableness rather than the skill's description.
///
/// **Repetition.** Each prompt is asked <c>runs</c> times. Temperature is zero, which reduces variation without
/// removing it, so one answer is an anecdote. The result is <c>k</c> of <c>n</c>, and the threshold is the author's.
/// </remarks>
public sealed class ActivationProber
{
    private const string ChoiceInstruction =
        "You route a user's request to at most one skill. Reply with the exact name of the single most appropriate "
        + "skill, or the word none if no skill applies. Reply with the name only, nothing else.";

    private readonly IModelRunner _runner;

    /// <summary>Initialises the prober.</summary>
    /// <param name="runner">The model to ask.</param>
    public ActivationProber(IModelRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    /// <summary>
    /// Runs every prompt in the expectation against the model.
    /// </summary>
    /// <param name="skill">The skill under test.</param>
    /// <param name="distractors">
    /// Other skills to offer alongside it, as name and description. Usually its siblings in the same collection.
    /// </param>
    /// <param name="expectation">The prompts, run count and threshold the author declared.</param>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>The outcomes, with the model's identity and what it cost.</returns>
    public async Task<ModelActivationReport> ProbeAsync(
        SkillDefinition skill,
        IReadOnlyList<SkillCandidate> distractors,
        ModelActivationExpectation expectation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(distractors);
        ArgumentNullException.ThrowIfNull(expectation);

        var catalogue = Catalogue(skill, distractors);
        var outcomes = new List<ModelActivationOutcome>(expectation.PromptCount);
        var requests = 0;
        var promptTokens = 0;
        var completionTokens = 0;

        foreach (var (prompt, expectedToFire) in Prompts(expectation))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chosen = 0;

            for (var run = 0; run < expectation.Runs; run++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var completion = await _runner
                    .CompleteAsync(new ModelPrompt(catalogue, prompt), cancellationToken)
                    .ConfigureAwait(false);

                requests++;
                promptTokens += completion.PromptTokens;
                completionTokens += completion.CompletionTokens;

                if (NamesTheSkill(completion.Text, skill.Name))
                {
                    chosen++;
                }
            }

            outcomes.Add(new ModelActivationOutcome(
                prompt,
                expectedToFire,
                chosen,
                expectation.Runs,
                expectation.Threshold));
        }

        return new ModelActivationReport(
            _runner.Identity,
            [.. distractors.Select(candidate => candidate.Name)],
            outcomes,
            requests,
            promptTokens,
            completionTokens);
    }

    private static IEnumerable<(string Prompt, bool ExpectedToFire)> Prompts(
        ModelActivationExpectation expectation) =>
        expectation.ShouldFire.Select(prompt => (prompt, true))
            .Concat(expectation.ShouldNotFire.Select(prompt => (prompt, false)));

    /// <summary>
    /// Builds the list of skills the model chooses between: the skill under test and its distractors, by name and
    /// description, in the order given so a run is reproducible.
    /// </summary>
    private static string Catalogue(SkillDefinition skill, IReadOnlyList<SkillCandidate> distractors)
    {
        var entries = new[] { new SkillCandidate(skill.Name, skill.Description) }
            .Concat(distractors)
            .Select(candidate => $"- {candidate.Name}: {candidate.Description}");

        return $"{ChoiceInstruction}\n\nAvailable skills:\n{string.Join('\n', entries)}";
    }

    /// <summary>
    /// Decides whether a reply chose the skill.
    /// </summary>
    /// <remarks>
    /// Deliberately strict about the name and forgiving about everything else: models add punctuation, backticks and
    /// the occasional sentence however firmly they are told not to. What it will not do is treat a reply that merely
    /// mentions the skill inside a refusal as a choice, which is why the name has to appear as its own token.
    /// </remarks>
    private static bool NamesTheSkill(string reply, string skillName) =>
        reply
            .Split([' ', '\n', '\r', '\t', '`', '"', '\'', '.', ',', ':', ';', '*', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, skillName, StringComparison.OrdinalIgnoreCase));
}
