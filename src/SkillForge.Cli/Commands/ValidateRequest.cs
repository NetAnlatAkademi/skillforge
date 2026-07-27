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
internal sealed record ValidateRequest(
    string Path,
    bool Strict,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions,
    IReadOnlyList<string> SuppressedCodes)
{
    /// <summary>Suppressing nothing, which is what every command other than <c>validate</c> wants.</summary>
    internal ValidateRequest(
        string path,
        bool strict,
        string format,
        string? outputPath,
        ReportRenderOptions renderOptions)
        : this(path, strict, format, outputPath, renderOptions, [])
    {
    }
}
