using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>update analyze</c> was asked to do.
/// </summary>
/// <param name="BasePath">The version the update starts from.</param>
/// <param name="TargetPath">The version it arrives at.</param>
/// <param name="FailOnExpansion">
/// Whether an update that reaches further should fail the run. Off by default, like every other comparison here:
/// whether a wider reach is acceptable is the organisation's decision, and the command's job is to make it visible.
/// </param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record UpdateAnalyzeRequest(
    string BasePath,
    string TargetPath,
    bool FailOnExpansion,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
