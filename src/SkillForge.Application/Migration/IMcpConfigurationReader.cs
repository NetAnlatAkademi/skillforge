using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// Reads MCP server declarations out of one configuration format.
/// </summary>
/// <remarks>
/// A format, not a provider: Claude Code and Cursor both declare servers in JSON, Codex in TOML, and a reader
/// says which files it can handle rather than which tool wrote them.
/// </remarks>
public interface IMcpConfigurationReader
{
    /// <summary>
    /// Determines whether this reader handles the given file.
    /// </summary>
    /// <param name="path">Path of the configuration file.</param>
    /// <returns><see langword="true"/> when this reader understands the format.</returns>
    bool CanRead(string path);

    /// <summary>
    /// Reads every MCP server the file declares.
    /// </summary>
    /// <param name="path">Path of the configuration file.</param>
    /// <param name="providerId">Provider to attribute the declarations to.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The servers declared, and a diagnostic instead when the file could not be read or parsed. A file with no
    /// MCP section is a success with no servers — not every configuration declares one.
    /// </returns>
    Task<McpConfigurationReadResult> ReadAsync(
        string path,
        string providerId,
        CancellationToken cancellationToken = default);
}
