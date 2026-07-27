using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge eval</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory, or the path of its <c>SKILL.md</c>.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write machine-readable output to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record EvalRequest(
    string Path,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
