namespace SkillForge.Domain.Evaluation;

/// <summary>
/// One eval case and how its assertions turned out.
/// </summary>
/// <param name="Name">The case's name.</param>
/// <param name="Assertions">Every assertion the case made, in the order they were declared.</param>
/// <param name="Skipped">
/// Whether the case asserted nothing. A case with no assertions is reported as skipped rather than passed: counting
/// it as a pass would make a suite look larger than it is, which is the one thing a test report must not do.
/// </param>
public sealed record EvalCaseResult(string Name, IReadOnlyList<EvalAssertion> Assertions, bool Skipped = false)
{
    /// <summary>Gets a value indicating whether every assertion in the case held.</summary>
    public bool Passed => !Skipped && Assertions.All(assertion => assertion.Passed);

    /// <summary>Gets the assertions that did not hold.</summary>
    public IReadOnlyList<EvalAssertion> Failures =>
        [.. Assertions.Where(assertion => !assertion.Passed)];
}
