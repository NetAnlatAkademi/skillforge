namespace SkillForge.Domain.Evaluation;

/// <summary>
/// The result of running a skill's evals.
/// </summary>
/// <param name="SkillName">The skill that was evaluated.</param>
/// <param name="SkillPath">Where it lives.</param>
/// <param name="Cases">Every case that ran.</param>
public sealed record EvalReport(string SkillName, string SkillPath, IReadOnlyList<EvalCaseResult> Cases)
{
    /// <summary>Gets the number of cases whose assertions all held.</summary>
    public int PassedCount => Cases.Count(result => result.Passed);

    /// <summary>Gets the number of cases with at least one failing assertion.</summary>
    public int FailedCount => Cases.Count(result => !result.Passed && !result.Skipped);

    /// <summary>Gets the number of cases that asserted nothing.</summary>
    public int SkippedCount => Cases.Count(result => result.Skipped);

    /// <summary>
    /// Gets a value indicating whether the run should be treated as a success.
    /// </summary>
    /// <remarks>
    /// A suite with no cases is **not** a pass. An empty evals folder means the author has not written any evals, and
    /// reporting that as green would tell them the opposite of the truth.
    /// </remarks>
    public bool Passed => Cases.Count > 0 && FailedCount == 0;
}
