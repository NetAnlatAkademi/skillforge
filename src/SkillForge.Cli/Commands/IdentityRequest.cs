using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>identity inspect</c> was asked to do.
/// </summary>
/// <param name="Path">MCP configuration file to read.</param>
/// <param name="Probe">
/// Whether to ask each HTTP server about itself. The only part of the command that leaves the machine, and the
/// only way a scope or an issuer can be observed at all. stdio servers are never launched.
/// </param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record IdentityInspectRequest(
    string Path,
    bool Probe,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);

/// <summary>
/// Everything <c>identity diff</c> was asked to do.
/// </summary>
/// <param name="BeforePath">The earlier configuration.</param>
/// <param name="AfterPath">The later configuration.</param>
/// <param name="Probe">Whether to ask each HTTP server about itself, on both sides.</param>
/// <param name="FailOnDrift">Whether a widened identity should fail the run.</param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record IdentityDiffRequest(
    string BeforePath,
    string AfterPath,
    bool Probe,
    bool FailOnDrift,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
