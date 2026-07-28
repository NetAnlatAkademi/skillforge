using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// Reads MCP declarations from whichever of a provider's configuration files exist.
/// </summary>
/// <remarks>
/// Shared by the adapters so that picking a reader for a format happens once. A file that exists in a format no
/// reader understands is reported as SF1015 rather than skipped: the alternative is an inventory that looks
/// complete and is not.
/// </remarks>
public sealed class McpConfigurationScanner
{
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IMcpConfigurationReader> _readers;

    /// <summary>Initialises the scanner.</summary>
    /// <param name="fileSystem">Used to test which configuration files exist.</param>
    /// <param name="readers">The formats SkillForge can read.</param>
    public McpConfigurationScanner(IFileSystem fileSystem, IEnumerable<IMcpConfigurationReader> readers)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(readers);

        _fileSystem = fileSystem;
        _readers = [.. readers];
    }

    /// <summary>
    /// Reads every declaration from the given candidate paths.
    /// </summary>
    /// <param name="providerId">Provider to attribute the declarations to.</param>
    /// <param name="candidatePaths">Files to read, in the order they should be reported. Missing files are skipped.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The servers found and any file that could not be used.</returns>
    public async Task<McpConfigurationReadResult> ScanAsync(
        string providerId,
        IEnumerable<string> candidatePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(candidatePaths);

        var servers = new List<McpServerDeclaration>();
        var diagnostics = new List<Diagnostic>();

        foreach (var path in candidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_fileSystem.FileExists(path))
            {
                continue;
            }

            var reader = _readers.FirstOrDefault(candidate => candidate.CanRead(path));

            if (reader is null)
            {
                diagnostics.AddRange(
                    McpConfigurationReadResult.Unreadable(path, "SkillForge has no reader for this format")
                        .Diagnostics);
                continue;
            }

            var result = await reader.ReadAsync(path, providerId, cancellationToken).ConfigureAwait(false);

            servers.AddRange(result.Servers);
            diagnostics.AddRange(result.Diagnostics);
        }

        return new McpConfigurationReadResult(servers, diagnostics);
    }
}
