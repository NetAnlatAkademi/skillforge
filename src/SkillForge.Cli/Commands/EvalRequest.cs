using SkillForge.Application.Abstractions;
using SkillForge.Domain.Modeling;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge eval</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory, or the path of its <c>SKILL.md</c>.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write machine-readable output to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
/// <param name="Model">
/// The model to ask for <c>model_activation</c> cases, or <see langword="null"/> to leave them unrun. Nothing is sent
/// anywhere unless this is set, and it is only ever set because the caller passed the flags.
/// </param>
/// <param name="MaxModelRequests">
/// The most model requests this run may make. A guard rather than a preference: a suite of ten prompts at ten runs is
/// a hundred requests, and somebody should find that out before paying for it, not after.
/// </param>
internal sealed record EvalRequest(
    string Path,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions,
    ModelSettings? Model = null,
    int MaxModelRequests = EvalRequest.DefaultMaxModelRequests)
{
    /// <summary>Enough for a handful of prompts at the default run count; a wall, not a target.</summary>
    internal const int DefaultMaxModelRequests = 100;
}
