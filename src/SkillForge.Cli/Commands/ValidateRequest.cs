using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>validate</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory or <c>SKILL.md</c> path.</param>
/// <param name="Strict">When set, warnings fail as well as errors.</param>
/// <param name="Format">One of <see cref="OutputFormat"/>.</param>
/// <param name="OutputPath">File to write machine-readable output to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record ValidateRequest(
    string Path,
    bool Strict,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
