using SkillForge.Application.Providers;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration.Adapters;

/// <summary>
/// Reads a GitHub Copilot installation.
/// </summary>
/// <remarks>
/// <c>~/.copilot</c> and its <c>config.json</c> were verified present on 2026-07-28, and that file turned out to
/// be JSON **with <c>//</c> comments** — which is why the JSON reader tolerates them rather than treating a
/// comment as a parse failure. No MCP configuration existed there to check against, so
/// <c>~/.copilot/mcp-config.json</c> and a project's <c>.vscode/mcp.json</c> are documented conventions rather
/// than verified paths; an unverified path that is wrong is simply never found.
///
/// The project-scoped instruction file, <c>.github/copilot-instructions.md</c>, is the one convention here that is
/// unambiguous.
/// </remarks>
public sealed class GitHubCopilotToolAdapter : IAgentToolAdapter
{
    private readonly AgentToolScanner _scanner;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="scanner">The shared scans.</param>
    public GitHubCopilotToolAdapter(AgentToolScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    /// <inheritdoc />
    public string ProviderId => GitHubCopilotProvider.Id;

    /// <inheritdoc />
    public async Task<AgentToolScan> ScanAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var home = Path.Combine(request.UserDirectory, ".copilot");
        var userMcp = Path.Combine(home, "mcp-config.json");
        var project = request.ProjectDirectory;
        var projectMcp = project is null ? null : Path.Combine(project, ".vscode", "mcp.json");

        var found = _scanner.ExistingPaths(
            home,
            Path.Combine(home, "config.json"),
            userMcp,
            projectMcp);

        var mcp = await _scanner.McpServers
            .ScanAsync(
                ProviderId,
                projectMcp is null ? [userMcp] : [userMcp, projectMcp],
                cancellationToken)
            .ConfigureAwait(false);

        var instructions = _scanner.InstructionFiles.Scan(ProviderId, Candidates(project));

        return new AgentToolScan(
            AgentToolScanner.Presence(ProviderId, GitHubCopilotProvider.Profile.DisplayName, found),
            [],
            mcp.Servers,
            instructions,
            mcp.Diagnostics);
    }

    private static IEnumerable<(string Path, InstructionScope Scope)> Candidates(string? project)
    {
        if (project is not null)
        {
            yield return (Path.Combine(project, ".github", "copilot-instructions.md"), InstructionScope.Project);
        }
    }
}
