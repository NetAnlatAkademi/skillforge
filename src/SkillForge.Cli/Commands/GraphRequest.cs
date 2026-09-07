using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>graph</c> was asked to do.
/// </summary>
/// <param name="Path">Directory to read.</param>
/// <param name="Format">
/// Console, JSON or Mermaid. SARIF is not offered and will not be: SARIF is a findings format, and a graph is a
/// description. Uploading a description as a set of static-analysis results would put a hundred "issues" on a pull
/// request that nobody claimed were problems.
/// </param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record GraphRequest(
    string Path,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
