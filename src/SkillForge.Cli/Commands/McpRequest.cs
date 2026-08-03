using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>mcp inspect</c> and <c>mcp validate</c> were asked to do.
/// </summary>
/// <param name="Path">The MCP configuration file to read.</param>
/// <param name="Probe">Whether to ask each HTTP server about itself.</param>
/// <param name="Gate">
/// Whether a finding should fail the run. <c>mcp validate</c> sets it; <c>mcp inspect</c> does not. The severity of
/// an <c>SF8xxx</c> finding is unchanged either way — what a command does with a finding is the command's decision,
/// and a published code's meaning is not.
/// </param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record McpRequest(
    string Path,
    bool Probe,
    bool Gate,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);

/// <summary>
/// Everything <c>mcp diff</c> was asked to do.
/// </summary>
/// <param name="BeforePath">The earlier configuration.</param>
/// <param name="AfterPath">The later configuration.</param>
/// <param name="FailOnChange">Fail on any change, not only on a file that could not be read.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record McpDiffRequest(
    string BeforePath,
    string AfterPath,
    bool FailOnChange,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
