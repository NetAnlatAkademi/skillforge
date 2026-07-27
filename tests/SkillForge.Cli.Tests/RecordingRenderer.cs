using SkillForge.Application.Abstractions;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Tests;

/// <summary>
/// Captures what a command runner asked to be presented, instead of writing it anywhere.
/// </summary>
/// <remarks>
/// One shared fake rather than a private copy per test class. Four identical copies existed until adding
/// <see cref="IValidationReportRenderer.RenderRun"/> meant editing all four to say the same thing.
/// </remarks>
internal sealed class RecordingRenderer : IValidationReportRenderer
{
    /// <summary>The last single-skill report handed to the renderer, if any.</summary>
    internal ValidationReport? Rendered { get; private set; }

    /// <summary>The last batch run handed to the renderer, if any.</summary>
    internal ValidationRun? RenderedRun { get; private set; }

    public void Render(ValidationReport report, ReportRenderOptions options) => Rendered = report;

    public void RenderRun(ValidationRun run, ReportRenderOptions options) => RenderedRun = run;
}
