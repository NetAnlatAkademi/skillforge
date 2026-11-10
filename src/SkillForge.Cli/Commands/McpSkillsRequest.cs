using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>mcp skills inspect</c> and <c>mcp skills verify</c> were asked to do.
/// </summary>
/// <param name="Path">The MCP configuration file to read.</param>
/// <param name="Probe">Whether to ask any server anything. Without it nothing leaves the machine.</param>
/// <param name="Verify">Whether to fetch and verify each skill, rather than only list them.</param>
/// <param name="SkillUris">Skills to verify by URI in addition to the listed ones.</param>
/// <param name="FailOnMismatch">Whether an integrity finding should fail the run. <c>verify</c> only.</param>
/// <param name="Format">Console, JSON, or — for <c>verify</c> — SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record McpSkillsRequest(
    string Path,
    bool Probe,
    bool Verify,
    IReadOnlyList<string> SkillUris,
    bool FailOnMismatch,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
