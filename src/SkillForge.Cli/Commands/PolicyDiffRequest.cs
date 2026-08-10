using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>policy diff</c> was asked to do.
/// </summary>
/// <param name="BeforePath">The earlier policy file.</param>
/// <param name="AfterPath">The later policy file.</param>
/// <param name="FailOnWeakening">
/// Whether a relaxed rule should fail the run. Off by default, like every other diff in the tool: whether a
/// widened policy is acceptable is a decision the organisation owns, and the command's job is to make it visible.
/// </param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record PolicyDiffRequest(
    string BeforePath,
    string AfterPath,
    bool FailOnWeakening,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
