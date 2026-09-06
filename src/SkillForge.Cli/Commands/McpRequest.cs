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

/// <summary>
/// Everything <c>mcp surface</c> was asked to do.
/// </summary>
/// <param name="Path">The MCP configuration file to read.</param>
/// <param name="Probe">
/// Whether to ask each HTTP server about itself. Without it there is nothing to count: a tool list comes from the
/// server, and SkillForge does not speak to anything unless it is told to.
/// </param>
/// <param name="PolicyPath">
/// Policy file to read <c>mcp.surface</c> thresholds from. A file that is not there is not an error — the
/// documented defaults apply and the report says they are defaults — but one that cannot be parsed fails the run,
/// for the same reason <c>policy check</c> refuses to continue without its rules.
/// </param>
/// <param name="FailOnThreshold">Whether a surface at or above the warning count should fail the run.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record McpSurfaceRequest(
    string Path,
    bool Probe,
    string PolicyPath,
    bool FailOnThreshold,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
