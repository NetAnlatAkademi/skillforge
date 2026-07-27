using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>inspect</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory or <c>SKILL.md</c> path.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="ShowFiles">Show the file inventory.</param>
/// <param name="ShowLinks">Show external URLs.</param>
/// <param name="ShowPermissions">Show inferred capabilities and declared tools.</param>
/// <param name="RenderOptions">How to present a load failure.</param>
internal sealed record InspectRequest(
    string Path,
    string Format,
    string? OutputPath,
    bool ShowFiles,
    bool ShowLinks,
    bool ShowPermissions,
    ReportRenderOptions RenderOptions);
