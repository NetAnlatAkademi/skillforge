using SkillForge.Application.Abstractions;
using SkillForge.Application.Migration;
using SkillForge.Domain.Migration;
using Tomlyn;
using Tomlyn.Model;

namespace SkillForge.Infrastructure.Migration;

/// <summary>
/// Reads MCP servers from Codex's <c>config.toml</c>, where they are <c>[mcp_servers.&lt;name&gt;]</c> tables.
/// </summary>
/// <remarks>
/// Tomlyn rather than a hand-written scanner. The tables on a real machine mix quoted and literal strings, empty
/// arrays and nested <c>[mcp_servers.&lt;name&gt;.env]</c> tables, and a scanner that mishandled one of those forms
/// would drop a server from an inventory silently — which is the one failure this command must not have.
///
/// Environment variable **values are never read**, exactly as in the JSON reader.
/// </remarks>
public sealed class TomlMcpConfigurationReader : IMcpConfigurationReader
{
    private const string ServersTableName = "mcp_servers";

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">Used to read the file.</param>
    public TomlMcpConfigurationReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public bool CanRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<McpConfigurationReadResult> ReadAsync(
        string path,
        string providerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(providerId);

        string content;

        try
        {
            content = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return McpConfigurationReadResult.Unreadable(path, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return McpConfigurationReadResult.Unreadable(path, exception.Message);
        }

        // Deserialize rather than TryDeserialize: the try-form reports "false" without saying why, and SF1015's
        // whole job is to tell the user what stopped SkillForge reading their configuration.
        TomlTable model;

        try
        {
            model = TomlSerializer.Deserialize<TomlTable>(content) ?? [];
        }
        catch (TomlException exception)
        {
            return McpConfigurationReadResult.Unreadable(path, exception.Message);
        }

        return McpConfigurationReadResult.Found(ReadServers(model, providerId, path));
    }

    private static IReadOnlyList<McpServerDeclaration> ReadServers(
        TomlTable root,
        string providerId,
        string path)
    {
        if (!root.TryGetValue(ServersTableName, out var section) || section is not TomlTable servers)
        {
            return [];
        }

        return
        [
            .. servers
                .Where(entry => entry.Value is TomlTable)
                .Select(entry => ReadServer(entry.Key, (TomlTable)entry.Value, providerId, path))
                .OrderBy(server => server.Name, StringComparer.Ordinal),
        ];
    }

    private static McpServerDeclaration ReadServer(
        string name,
        TomlTable server,
        string providerId,
        string path)
    {
        var command = Text(server, "command");
        var url = Text(server, "url");

        return new McpServerDeclaration(
            name,
            providerId,
            Transport(command, url),
            command ?? url,
            Arguments(server),
            EnvironmentVariableNames(server),
            path);
    }

    private static McpTransport Transport(string? command, string? url) => (command, url) switch
    {
        (not null, _) => McpTransport.Stdio,
        (_, not null) => McpTransport.Http,
        _ => McpTransport.Unknown,
    };

    private static string? Text(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) && value is string text ? text : null;

    private static IReadOnlyList<string> Arguments(TomlTable server) =>
        server.TryGetValue("args", out var value) && value is TomlArray args
            ? [.. args.OfType<string>()]
            : [];

    /// <summary>Names only. See the remarks on this class.</summary>
    private static IReadOnlyList<string> EnvironmentVariableNames(TomlTable server) =>
        server.TryGetValue("env", out var value) && value is TomlTable env
            ? [.. env.Keys.OrderBy(name => name, StringComparer.Ordinal)]
            : [];
}
