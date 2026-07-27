using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Validation;

/// <summary>
/// The outcome of validating every skill in a directory.
/// </summary>
/// <param name="RootPath">Directory that was searched.</param>
/// <param name="Skills">One report per skill found, in discovery order.</param>
/// <param name="Summary">Counts across every skill.</param>
public sealed record ValidationRun(
    string RootPath,
    IReadOnlyList<ValidationReport> Skills,
    ValidationSummary Summary)
{
    /// <summary>Gets a value indicating whether every skill is usable.</summary>
    public bool IsValid => Summary.IsValid;

    /// <summary>Gets the number of skills that were validated.</summary>
    public int SkillCount => Skills.Count;

    /// <summary>Gets the number of skills with at least one error.</summary>
    public int InvalidSkillCount => Skills.Count(skill => !skill.IsValid);

    /// <summary>
    /// Determines whether the run should be treated as a failure.
    /// </summary>
    /// <param name="strict">When <see langword="true"/>, warnings fail as well as errors.</param>
    /// <returns><see langword="true"/> when any skill in the run failed.</returns>
    /// <remarks>
    /// One bad skill fails the run. A batch that reported success because most of its skills were fine would
    /// be useless as a build gate.
    /// </remarks>
    public bool HasFailed(bool strict) => Skills.Any(skill => skill.HasFailed(strict));

    /// <summary>
    /// Builds a run from the reports of the skills that were found.
    /// </summary>
    /// <param name="rootPath">Directory that was searched.</param>
    /// <param name="reports">One report per skill.</param>
    /// <returns>The run, with its summary totalled across every skill.</returns>
    public static ValidationRun From(string rootPath, IReadOnlyList<ValidationReport> reports)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(reports);

        var diagnostics = reports.SelectMany(report => report.Diagnostics).ToArray();

        return new ValidationRun(rootPath, reports, ValidationSummary.FromDiagnostics(diagnostics));
    }
}
