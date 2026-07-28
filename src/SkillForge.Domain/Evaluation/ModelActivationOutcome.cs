namespace SkillForge.Domain.Evaluation;

/// <summary>
/// What a model actually did with one prompt, over several runs.
/// </summary>
/// <param name="Prompt">The prompt that was put to it.</param>
/// <param name="ExpectedToFire">Whether the skill was expected to be chosen.</param>
/// <param name="ChosenRuns">How many runs chose the skill.</param>
/// <param name="Runs">How many runs were made.</param>
/// <param name="Threshold">The share of agreeing runs the author asked for.</param>
public sealed record ModelActivationOutcome(
    string Prompt,
    bool ExpectedToFire,
    int ChosenRuns,
    int Runs,
    double Threshold)
{
    /// <summary>Gets the share of runs that chose the skill.</summary>
    public double ChosenRate => Runs == 0 ? 0 : (double)ChosenRuns / Runs;

    /// <summary>Gets the share of runs that agreed with the expectation.</summary>
    public double AgreementRate => ExpectedToFire ? ChosenRate : 1 - ChosenRate;

    /// <summary>
    /// Gets a value indicating whether the outcome met the declared threshold.
    /// </summary>
    /// <remarks>
    /// Not a bare pass: the rate is reported alongside it, always. "Passed" hides the difference between 10 of 10 and
    /// 8 of 10, and that difference is most of what an author needs to know.
    /// </remarks>
    public bool Met => Runs > 0 && AgreementRate >= Threshold;
}
