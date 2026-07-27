using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>pack</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory or <c>SKILL.md</c> path.</param>
/// <param name="OutputDirectory">Where to write the artefacts.</param>
/// <param name="VersionOverride">Version to package as, or <see langword="null"/> for the declared one.</param>
/// <param name="SkipValidation">Package even when validation finds errors.</param>
/// <param name="RenderOptions">How to present output.</param>
internal sealed record PackRequest(
    string Path,
    string OutputDirectory,
    string? VersionOverride,
    bool SkipValidation,
    ReportRenderOptions RenderOptions);
