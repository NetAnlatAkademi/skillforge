using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Migration;
using SkillForge.Domain.Migration;

namespace SkillForge.Infrastructure.Migration;

/// <summary>
/// Reads MCP servers from a JSON configuration — Claude Code's <c>~/.claude.json</c> and <c>.mcp.json</c>,
/// Cursor's and VS Code's <c>mcp.json</c>.
/// </summary>
/// <remarks>
/// Comments and trailing commas are allowed. That is not tidiness: <c>~/.copilot/config.json</c> on a real
/// machine begins with two <c>//</c> lines, and a strict parser reports a working configuration as corrupt.
///
/// Environment variable **values are never read**. The reader takes the property names out of the <c>env</c>
/// object and drops the values on the floor, because an MCP declaration is one of the likeliest places in a home
/// directory to hold an API token and the safest filter is the one that never has the value to begin with.
/// </remarks>
public sealed class JsonMcpConfigurationReader : IMcpConfigurationReader
{
    private static readonly JsonDocumentOptions ForgivingJson = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">Used to read the file.</param>
    public JsonMcpConfigurationReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public bool CanRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
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

        try
        {
            using var document = JsonDocument.Parse(content, ForgivingJson);

            return McpConfigurationReadResult.Found(ReadServers(document.RootElement, providerId, path));
        }
        catch (JsonException exception)
        {
            return McpConfigurationReadResult.Unreadable(path, exception.Message);
        }
    }

    /// <summary>
    /// Reads the <c>mcpServers</c> object, which sits at the root in every JSON layout SkillForge has seen. A file
    /// without one declares no servers, which is ordinary rather than an error.
    /// </summary>
    private static IReadOnlyList<McpServerDeclaration> ReadServers(
        JsonElement root,
        string providerId,
        string path)
    {
        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty("mcpServers", out var servers)
            || servers.ValueKind is not JsonValueKind.Object)
        {
            return [];
        }

        return
        [
            .. servers.EnumerateObject()
                .Where(server => server.Value.ValueKind is JsonValueKind.Object)
                .Select(server => ReadServer(server, providerId, path))
                .OrderBy(server => server.Name, StringComparer.Ordinal),
        ];
    }

    private static McpServerDeclaration ReadServer(
        JsonProperty server,
        string providerId,
        string path)
    {
        var command = Text(server.Value, "command");
        var url = Text(server.Value, "url");

        return new McpServerDeclaration(
            server.Name,
            providerId,
            Transport(server.Value, command, url),
            command ?? url,
            Arguments(server.Value),
            EnvironmentVariableNames(server.Value),
            path);
    }

    /// <summary>
    /// Prefers what the file says over what can be inferred: a declared <c>type</c> is the author's own statement,
    /// and only when it says nothing is the transport read from whether a command or a URL is present.
    /// </summary>
    private static McpTransport Transport(JsonElement server, string? command, string? url) =>
        Text(server, "type")?.ToLowerInvariant() switch
        {
            "stdio" => McpTransport.Stdio,
            "http" or "sse" or "streamable-http" or "http-stream" => McpTransport.Http,
            _ when command is not null => McpTransport.Stdio,
            _ when url is not null => McpTransport.Http,
            _ => McpTransport.Unknown,
        };

    private static string? Text(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> Arguments(JsonElement server) =>
        server.TryGetProperty("args", out var args) && args.ValueKind is JsonValueKind.Array
            ? [.. args.EnumerateArray()
                .Where(argument => argument.ValueKind is JsonValueKind.String)
                .Select(argument => argument.GetString()!)]
            : [];

    /// <summary>Names only. See the remarks on this class.</summary>
    private static IReadOnlyList<string> EnvironmentVariableNames(JsonElement server) =>
        server.TryGetProperty("env", out var env) && env.ValueKind is JsonValueKind.Object
            ? [.. env.EnumerateObject()
                .Select(variable => variable.Name)
                .OrderBy(name => name, StringComparer.Ordinal)]
            : [];
}
