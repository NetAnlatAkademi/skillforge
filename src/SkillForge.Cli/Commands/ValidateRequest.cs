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
/// <param name="SuppressedCodes">
/// Codes the caller does not want reported. Combined with whatever each skill's own <c>skillforge.yaml</c>
/// suppresses, so a repository-wide flag and a per-skill decision add up rather than overriding each other.
/// </param>
/// <param name="Providers">
/// Providers to check the skill against even though it does not declare them — "would this work on Codex?".
/// Additional to the skill's own <c>compatibility</c> list, never instead of it.
/// </param>
internal sealed record ValidateRequest(
    string Path,
    bool Strict,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions,
    IReadOnlyList<string> SuppressedCodes,
    IReadOnlyList<string> Providers)
{
    /// <summary>Suppressing nothing, which is what every command other than <c>validate</c> wants.</summary>
    internal ValidateRequest(
        string path,
        bool strict,
        string format,
        string? outputPath,
        ReportRenderOptions renderOptions)
        : this(path, strict, format, outputPath, renderOptions, [], [])
    {
    }
}
