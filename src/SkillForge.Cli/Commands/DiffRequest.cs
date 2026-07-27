using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>diff</c> was asked to do.
/// </summary>
/// <param name="BeforePath">The earlier version: a skill directory or a <c>SKILL.md</c> path.</param>
/// <param name="AfterPath">The later version.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="FailOnChange">
/// Fail on any surface change, not only on a new error. Off by default: whether a new permission is acceptable is
/// a policy SkillForge does not own, so it is the caller who decides that a change alone should stop a build.
/// </param>
/// <param name="RenderOptions">How to present a side that could not be loaded.</param>
internal sealed record DiffRequest(
    string BeforePath,
    string AfterPath,
    string Format,
    string? OutputPath,
    bool FailOnChange,
    ReportRenderOptions RenderOptions);
