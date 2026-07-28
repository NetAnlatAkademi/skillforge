using SkillForge.Application.Providers;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration.Adapters;

/// <summary>
/// Reads a Codex installation.
/// </summary>
/// <remarks>
/// Paths verified against a working installation on 2026-07-28: <c>~/.codex/skills</c>, and MCP servers as
/// <c>[mcp_servers.&lt;name&gt;]</c> tables in <c>~/.codex/config.toml</c> — the one provider here that does not
/// use JSON, which is why the reader is chosen by format rather than by provider.
///
/// <c>~/.codex/auth.json</c> is never read, for the same reason as Claude Code's credentials file.
/// </remarks>
public sealed class CodexToolAdapter : IAgentToolAdapter
{
    private readonly AgentToolScanner _scanner;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="scanner">The shared scans.</param>
    public CodexToolAdapter(AgentToolScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    /// <inheritdoc />
    public string ProviderId => CodexProvider.Id;

    /// <inheritdoc />
    public async Task<AgentToolScan> ScanAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var home = Path.Combine(request.UserDirectory, ".codex");
        var config = Path.Combine(home, "config.toml");
        var project = request.ProjectDirectory;

        var found = _scanner.ExistingPaths(home, config, Path.Combine(home, "skills"));

        var skills = await _scanner.Skills
            .ScanAsync(ProviderId, Path.Combine(home, "skills"), cancellationToken)
            .ConfigureAwait(false);

        var mcp = await _scanner.McpServers
            .ScanAsync(ProviderId, [config], cancellationToken)
            .ConfigureAwait(false);

        var instructions = _scanner.InstructionFiles.Scan(ProviderId, Candidates(home, project));

        return new AgentToolScan(
            AgentToolScanner.Presence(ProviderId, CodexProvider.Profile.DisplayName, found),
            skills,
            mcp.Servers,
            instructions,
            mcp.Diagnostics);
    }

    private static IEnumerable<(string Path, InstructionScope Scope)> Candidates(string home, string? project)
    {
        yield return (Path.Combine(home, "AGENTS.md"), InstructionScope.User);

        if (project is not null)
        {
            yield return (Path.Combine(project, "AGENTS.md"), InstructionScope.Project);
        }
    }
}
