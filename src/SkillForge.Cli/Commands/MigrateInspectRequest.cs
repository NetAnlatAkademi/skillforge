using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>migrate inspect</c> was asked to do.
/// </summary>
/// <param name="ProjectPath">
/// A project to include project-scoped configuration from, or <see langword="null"/> for user scope only.
/// </param>
/// <param name="UserDirectory">
/// The home directory to read, or <see langword="null"/> to use the current user's. Overridable so the command can
/// be pointed at an exported profile — and so its own tests do not depend on the machine they run on.
/// </param>
/// <param name="Format">One of <see cref="OutputFormat"/>. SARIF is not offered: an inventory is not a finding.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
/// <param name="ProbeMcpServers">
/// When set, ask each HTTP MCP server about itself with one <c>server/discover</c> request. Opt-in, because it is the
/// only part of this command that leaves the machine. stdio servers are never launched, whatever this says.
/// </param>
internal sealed record MigrateInspectRequest(
    string? ProjectPath,
    string? UserDirectory,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions,
    bool ProbeMcpServers = false);
