using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>policy check</c> was asked to do.
/// </summary>
/// <param name="Path">Skill, or directory of skills, to judge.</param>
/// <param name="PolicyPath">The policy file to judge them against.</param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record PolicyCheckRequest(
    string Path,
    string PolicyPath,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
