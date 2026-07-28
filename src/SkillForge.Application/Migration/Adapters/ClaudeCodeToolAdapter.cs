using SkillForge.Application.Providers;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration.Adapters;

/// <summary>
/// Reads a Claude Code installation.
/// </summary>
/// <remarks>
/// Paths verified against a working installation on 2026-07-28: skills under <c>~/.claude/skills</c>, MCP servers
/// in <c>~/.claude.json</c> under <c>mcpServers</c>, instructions in <c>~/.claude/CLAUDE.md</c> and
/// <c>~/.claude/AGENTS.md</c>. The project-scoped paths — <c>.mcp.json</c>, <c>CLAUDE.md</c>,
/// <c>.claude/skills</c> — are the documented conventions; which of them exist is reported, so a wrong guess
/// shows up as a path that is simply never found rather than as a false claim.
///
/// <c>~/.claude/.credentials.json</c> is never read. Nothing in this command needs a credential, so the safest
/// way to avoid printing one is to never open the file that holds them.
/// </remarks>
public sealed class ClaudeCodeToolAdapter : IAgentToolAdapter
{
    private readonly AgentToolScanner _scanner;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="scanner">The shared scans.</param>
    public ClaudeCodeToolAdapter(AgentToolScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    /// <inheritdoc />
    public string ProviderId => ClaudeCodeProvider.Id;

    /// <inheritdoc />
    public async Task<AgentToolScan> ScanAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var home = Path.Combine(request.UserDirectory, ".claude");
        var userMcp = Path.Combine(request.UserDirectory, ".claude.json");
        var project = request.ProjectDirectory;

        var found = _scanner.ExistingPaths(
            home,
            userMcp,
            Path.Combine(home, "settings.json"),
            project is null ? null : Path.Combine(project, ".claude"),
            project is null ? null : Path.Combine(project, ".mcp.json"));

        var skills = await _scanner.Skills
            .ScanAsync(ProviderId, Path.Combine(home, "skills"), cancellationToken)
            .ConfigureAwait(false);

        var projectSkills = project is null
            ? []
            : await _scanner.Skills
                .ScanAsync(ProviderId, Path.Combine(project, ".claude", "skills"), cancellationToken)
                .ConfigureAwait(false);

        var mcp = await _scanner.McpServers
            .ScanAsync(
                ProviderId,
                project is null ? [userMcp] : [userMcp, Path.Combine(project, ".mcp.json")],
                cancellationToken)
            .ConfigureAwait(false);

        var instructions = _scanner.InstructionFiles.Scan(ProviderId, Candidates(home, project));

        return new AgentToolScan(
            AgentToolScanner.Presence(ProviderId, ClaudeCodeProvider.Profile.DisplayName, found),
            [.. skills, .. projectSkills],
            mcp.Servers,
            instructions,
            mcp.Diagnostics);
    }

    private static IEnumerable<(string Path, InstructionScope Scope)> Candidates(string home, string? project)
    {
        yield return (Path.Combine(home, "CLAUDE.md"), InstructionScope.User);
        yield return (Path.Combine(home, "AGENTS.md"), InstructionScope.User);

        if (project is null)
        {
            yield break;
        }

        yield return (Path.Combine(project, "CLAUDE.md"), InstructionScope.Project);
        yield return (Path.Combine(project, ".claude", "CLAUDE.md"), InstructionScope.Project);
    }
}
