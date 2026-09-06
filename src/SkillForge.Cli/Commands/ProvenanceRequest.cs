using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>provenance</c> was asked to do.
/// </summary>
/// <param name="Path">Directory to scan.</param>
/// <param name="Format">Console or JSON. SARIF is not offered: a provenance report is not a set of findings.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record ProvenanceRequest(
    string Path,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);

/// <summary>
/// Everything <c>provenance diff</c> was asked to do.
/// </summary>
/// <param name="BeforePath">The earlier tree.</param>
/// <param name="AfterPath">The later tree.</param>
/// <param name="FailOnDrift">Whether a reported drift should fail the run.</param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record ProvenanceDiffRequest(
    string BeforePath,
    string AfterPath,
    bool FailOnDrift,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
