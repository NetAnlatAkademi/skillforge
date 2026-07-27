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

    /// <summary>Renders a run over several skills.</summary>
    /// <param name="run">The run to present.</param>
    /// <param name="options">How much to say and whether colour is allowed.</param>
    void RenderRun(ValidationRun run, ReportRenderOptions options);
}
