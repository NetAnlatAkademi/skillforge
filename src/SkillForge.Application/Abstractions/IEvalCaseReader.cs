using SkillForge.Domain;
using SkillForge.Domain.Evaluation;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Reads a skill's eval cases.
/// </summary>
/// <remarks>
/// An abstraction because the cases live in files and the runner must not: <c>EvalRunner</c> is a pure function of a
/// loaded skill and a list of cases, and this is what keeps it that way.
/// </remarks>
public interface IEvalCaseReader
{
    /// <summary>
    /// Reads every case declared under the skill's <c>evals</c> folder.
    /// </summary>
    /// <param name="skillDirectory">The skill's directory.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The cases, with diagnostics for any file that could not be read. A skill with no <c>evals</c> folder returns
    /// no cases and no diagnostics — not having evals is not a finding.
    /// </returns>
    Task<OperationResult<IReadOnlyList<EvalCase>>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default);
}
