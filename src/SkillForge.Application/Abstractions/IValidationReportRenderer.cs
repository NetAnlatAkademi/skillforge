using SkillForge.Domain.Validation;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Presents a validation report to the user.
/// </summary>
/// <remarks>
/// Implemented in the Reporting layer. Behind this seam the command classes neither know nor care whether
/// output ends up as coloured console text, JSON or SARIF.
/// </remarks>
public interface IValidationReportRenderer
{
    /// <summary>Renders a report.</summary>
    /// <param name="report">The report to present.</param>
    /// <param name="options">How much to say and whether colour is allowed.</param>
    void Render(ValidationReport report, ReportRenderOptions options);
}

/// <summary>
/// How output should be presented.
/// </summary>
/// <param name="Quiet">Show only the verdict and any errors.</param>
/// <param name="Verbose">Show the checks that passed as well as the findings.</param>
/// <param name="NoColor">Suppress colour and other ANSI output, for logs and pipes.</param>
public sealed record ReportRenderOptions(bool Quiet = false, bool Verbose = false, bool NoColor = false);
