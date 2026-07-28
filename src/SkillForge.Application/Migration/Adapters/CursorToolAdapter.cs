using SkillForge.Application.Providers;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration.Adapters;

/// <summary>
/// Reads a Cursor installation.
/// </summary>
/// <remarks>
/// **Nothing here was verified against a real installation** — Cursor was not installed on the machine the other
/// three adapters were checked on. The paths are its documented conventions: <c>~/.cursor/mcp.json</c> for
/// user-scoped MCP servers, <c>.cursor/mcp.json</c> and <c>.cursorrules</c> in a project. That gap is recorded in
/// <c>docs/migration.md</c> rather than left for somebody to discover, and the failure mode is mild: an unverified
/// path is simply never found, so the report says Cursor is absent rather than claiming something false about it.
///
/// No skill directory is looked for, because SkillForge has read nothing about where Cursor would keep one.
/// Guessing would produce an inventory heading that is permanently empty for the wrong reason.
/// </remarks>
public sealed class CursorToolAdapter : IAgentToolAdapter
{
    private readonly AgentToolScanner _scanner;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="scanner">The shared scans.</param>
    public CursorToolAdapter(AgentToolScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    /// <inheritdoc />
    public string ProviderId => CursorProvider.Id;

    /// <inheritdoc />
    public async Task<AgentToolScan> ScanAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var home = Path.Combine(request.UserDirectory, ".cursor");
        var userMcp = Path.Combine(home, "mcp.json");
        var project = request.ProjectDirectory;
        var projectMcp = project is null ? null : Path.Combine(project, ".cursor", "mcp.json");

        var found = _scanner.ExistingPaths(home, userMcp, projectMcp);

        var mcp = await _scanner.McpServers
            .ScanAsync(
                ProviderId,
                projectMcp is null ? [userMcp] : [userMcp, projectMcp],
                cancellationToken)
            .ConfigureAwait(false);

        var instructions = _scanner.InstructionFiles.Scan(ProviderId, Candidates(project));

        return new AgentToolScan(
            AgentToolScanner.Presence(ProviderId, CursorProvider.Profile.DisplayName, found),
            [],
            mcp.Servers,
            instructions,
            mcp.Diagnostics);
    }

    private static IEnumerable<(string Path, InstructionScope Scope)> Candidates(string? project)
    {
        if (project is not null)
        {
            yield return (Path.Combine(project, ".cursorrules"), InstructionScope.Project);
        }
    }
}
