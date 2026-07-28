using SkillForge.Domain.Modeling;

namespace SkillForge.Domain.Evaluation;

/// <summary>
/// Everything a model was asked about one skill, and what it answered.
/// </summary>
/// <remarks>
/// Kept out of <see cref="EvalReport"/>'s deterministic results and given no diagnostic codes. An <c>SFxxxx</c> code
/// means a fact somebody can see in a file; a model's answer is a sample from a distribution, and giving it the same
/// shape would invite it to be read the same way — including into SARIF, where it would arrive as a finding about
/// source code that nobody can verify by looking.
/// </remarks>
/// <param name="Model">Which model answered. Reported always; a rate without it is a rumour.</param>
/// <param name="Distractors">
/// The other skills offered alongside this one. Recorded because the result is only meaningful relative to what it was
/// competing against — asked alone, a model chooses almost anything.
/// </param>
/// <param name="Outcomes">One entry per prompt.</param>
/// <param name="RequestCount">How many requests were made.</param>
/// <param name="PromptTokens">Tokens the endpoint reported for the requests; 0 when it reported none.</param>
/// <param name="CompletionTokens">Tokens the endpoint reported for the replies; 0 when it reported none.</param>
public sealed record ModelActivationReport(
    ModelIdentity Model,
    IReadOnlyList<string> Distractors,
    IReadOnlyList<ModelActivationOutcome> Outcomes,
    int RequestCount,
    int PromptTokens,
    int CompletionTokens)
{
    /// <summary>Gets the outcomes that did not meet their threshold.</summary>
    public IEnumerable<ModelActivationOutcome> Unmet => Outcomes.Where(outcome => !outcome.Met);

    /// <summary>Gets a value indicating whether every outcome met its threshold.</summary>
    public bool AllMet => Outcomes.Count > 0 && Outcomes.All(outcome => outcome.Met);

    /// <summary>
    /// Gets a value indicating whether the result rests on distractors.
    /// </summary>
    /// <remarks>
    /// A probe with no distractors is reported with a warning in the output rather than suppressed: a skill offered
    /// on its own is chosen by almost any model for almost any prompt, so the number is weak evidence and the reader
    /// has to be told which kind they are holding.
    /// </remarks>
    public bool HadDistractors => Distractors.Count > 0;
}
