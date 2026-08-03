using SkillForge.Application.Abstractions;
using SkillForge.Application.Migration;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Inspects one MCP configuration file, named by the caller.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="McpConfigurationScanner"/>, which finds the files a provider owns. Here the file is
/// the argument: a repository's <c>.mcp.json</c>, a config under review in a pull request, a file exported from
/// somewhere else. Same readers, same declaration checks, same rule that a stdio server is never launched.
///
/// The declared provider is <c>"file"</c> rather than a guess. Which agent wrote a file the user named is not
/// knowable from its contents, and attributing it to Claude Code because the shape matches would be an invention.
/// </remarks>
public sealed class McpFileInspector
{
    /// <summary>The provider a file inspected by path is attributed to.</summary>
    public const string FileProviderId = "file";

    private readonly IReadOnlyList<IMcpConfigurationReader> _readers;
    private readonly McpDeclarationInspector _declarationInspector;
    private readonly McpProber _prober;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the inspector.</summary>
    /// <param name="readers">One reader per configuration format.</param>
    /// <param name="declarationInspector">Checks what a declaration says on its own.</param>
    /// <param name="prober">Asks HTTP servers about themselves, when the caller asks for it.</param>
    /// <param name="fileSystem">Used to establish that the file is there before anything else is said.</param>
    public McpFileInspector(
        IEnumerable<IMcpConfigurationReader> readers,
        McpDeclarationInspector declarationInspector,
        McpProber prober,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentNullException.ThrowIfNull(declarationInspector);
        ArgumentNullException.ThrowIfNull(prober);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _readers = [.. readers];
        _declarationInspector = declarationInspector;
        _prober = prober;
        _fileSystem = fileSystem;
    }

    /// <summary>Reads and inspects one configuration file.</summary>
    /// <param name="path">Path of the configuration file.</param>
    /// <param name="probe">Whether to ask each HTTP server about itself.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>What the file declares and what that reveals.</returns>
    public async Task<McpConfigurationInspection> InspectAsync(
        string path,
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var servers = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (servers.Diagnostics.Count > 0 && servers.Servers.Count == 0)
        {
            return new McpConfigurationInspection(path, [], servers.Diagnostics, []);
        }

        var findings = new List<Diagnostic>(servers.Diagnostics);
        foreach (var server in servers.Servers)
        {
            findings.AddRange(_declarationInspector.Inspect(server));
        }

        if (!probe)
        {
            return new McpConfigurationInspection(path, servers.Servers, Ordered(findings), []);
        }

        var outcome = await _prober.ProbeAsync(servers.Servers, cancellationToken).ConfigureAwait(false);
        findings.AddRange(outcome.Diagnostics);

        return new McpConfigurationInspection(path, servers.Servers, Ordered(findings), outcome.Probes);
    }

    /// <summary>
    /// Reads the declarations, or reports why it could not. A file with an extension no reader claims is reported
    /// as such rather than parsed hopefully as JSON: guessing the format of a file the user named is how a parse
    /// error ends up describing the wrong problem.
    /// </summary>
    private async Task<McpConfigurationReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(path))
        {
            return McpConfigurationReadResult.Unreadable(path, "the file does not exist");
        }

        var reader = _readers.FirstOrDefault(candidate => candidate.CanRead(path));
        if (reader is null)
        {
            return McpConfigurationReadResult.Unreadable(
                path,
                "no reader handles this file's format; MCP configurations are read from .json and .toml");
        }

        return await reader.ReadAsync(path, FileProviderId, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<Diagnostic> Ordered(IEnumerable<Diagnostic> findings) =>
        Validation.DiagnosticOrdering.Sort(findings);
}
