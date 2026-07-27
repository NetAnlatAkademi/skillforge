using SkillForge.Application.Abstractions;
using SkillForge.Domain.Skills;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>init</c> was asked to do.
/// </summary>
/// <param name="Directory">
/// Where to create the skill, or <see langword="null"/> to use the skill name as the directory.
/// </param>
/// <param name="Options">What to put in the generated files.</param>
/// <param name="RenderOptions">How to present output.</param>
internal sealed record InitRequest(
    string? Directory,
    SkillInitializationOptions Options,
    ReportRenderOptions RenderOptions);
