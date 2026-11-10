using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// Asks an HTTP MCP server about itself using the <c>2026-07-28</c> revision's <c>server/discover</c>.
/// </summary>
/// <remarks>
/// One request, which is why this revision is worth targeting first: <c>server/discover</c> is mandatory for servers
/// and returns supported versions, capabilities and identity together, so a whole inspection costs a single POST and no
/// session. Earlier revisions need the <c>initialize</c> handshake instead; that is a separate adapter, and its absence
/// is reported as SF8004 rather than guessed at.
///
/// Field names here are taken from the specification, not from a summary of it:
/// <c>result.supportedVersions</c>, <c>result.capabilities</c>, and
/// <c>result._meta['io.modelcontextprotocol/serverInfo']</c>. The request carries the protocol version in
/// <c>params._meta['io.modelcontextprotocol/protocolVersion']</c> and, on Streamable HTTP, the
/// <c>MCP-Protocol-Version</c> header.
/// </remarks>
public sealed class Mcp20260728ProtocolAdapter : IMcpProtocolAdapter
{
    private const string Revision = "2026-07-28";
    private const string ServerInfoKey = "io.modelcontextprotocol/serverInfo";

    /// <summary>JSON-RPC's "method not found", which is how a server without discovery answers.</summary>
    private const int MethodNotFound = -32601;

    private readonly HttpClient _client;
    private readonly McpJsonRpcChannel _channel;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="client">The client to send with. Timeouts and headers are the caller's to configure.</param>
    public Mcp20260728ProtocolAdapter(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _channel = new McpJsonRpcChannel(client);
    }

    /// <inheritdoc />
    public string ProtocolVersion => Revision;

    /// <inheritdoc />
    public async Task<McpServerProbe> ProbeAsync(
        McpServerDeclaration server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.Command is not { Length: > 0 } url)
        {
            return McpServerProbe.Failed(server.Name, McpProbeStatus.NotProbed, "the declaration names no URL");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(Body(), Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", Revision);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return McpServerProbe.Failed(server.Name, McpProbeStatus.Unreachable, Innermost(exception).Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return McpServerProbe.Failed(server.Name, McpProbeStatus.Unreachable, "it did not answer in time");
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // 404 and 405 are how an HTTP server that has never heard of this method answers, and the specification
            // points at the protocol-version header section for exactly this fallback.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            {
                return McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.NoDiscovery,
                    $"the endpoint answered {(int)response.StatusCode} to server/discover");
            }

            // A 401 is an answer, not a failure: the challenge is how a client learns to authorise, and reading it is
            // the whole of the authorization question that can be answered without credentials.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return McpServerProbe.NeedsAuthorization(
                    server.Name,
                    McpAuthorizationChallenge(response),
                    Revision);
            }

            if (!response.IsSuccessStatusCode)
            {
                return McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.Unreachable,
                    $"it answered {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var probe = Read(server.Name, payload);

            // Further requests, and only when the server said it has tools. Tool conformance cannot be checked
            // without the tool list, and the list cannot be had without asking — but a server that declares no
            // tools is never asked.
            if (probe.Status != McpProbeStatus.Answered
                || !probe.Capabilities.Contains("tools", StringComparer.OrdinalIgnoreCase))
            {
                return probe;
            }

            var listing = await ToolsAsync(url, cancellationToken).ConfigureAwait(false);

            return probe with { Tools = listing.Tools, Paging = listing.Paging };
        }
    }

    /// <summary>
    /// Reads the <c>WWW-Authenticate</c> challenge. The parameters are a comma-separated list of
    /// <c>name="value"</c> pairs, and the two that matter here are <c>resource_metadata</c> — where a client must look
    /// to find the authorization server — and <c>scope</c>.
    /// </summary>
    private static McpAuthorizationChallenge McpAuthorizationChallenge(HttpResponseMessage response)
    {
        var challenge = response.Headers.WwwAuthenticate.FirstOrDefault();

        return new McpAuthorizationChallenge(
            challenge?.Scheme ?? "unknown",
            Parameter(challenge?.Parameter, "resource_metadata"),
            Parameter(challenge?.Parameter, "scope"));
    }

    private static string? Parameter(string? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        foreach (var part in parameters.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && part[..separator].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separator + 1)..].Trim().Trim('"');
            }
        }

        return null;
    }

    /// <summary>
    /// Reads every page of <c>tools/list</c>, reducing each tool to the facts a conformance check needs.
    /// </summary>
    /// <remarks>
    /// The whole list, because a partial one cannot answer the question the tool list is read for. "This server
    /// exposes fifty tools" is a different sentence from "the first page of this server's tools holds fifty", and a
    /// declared-versus-runtime comparison built on the second would report every tool past page one as missing.
    ///
    /// The walk and its bounds are <see cref="McpCursorWalk"/>'s, shared with <c>skills/list</c>, so no list a server
    /// pages is walked under different rules. Failure is deliberately quiet: the probe already succeeded, and a server
    /// that answers discovery but refuses its tool list has told us something less interesting than what we already
    /// have. Pages read before a failure are kept, because they are true.
    /// </remarks>
    private async Task<McpToolListing> ToolsAsync(string url, CancellationToken cancellationToken)
    {
        var walk = await McpCursorWalk
            .WalkAsync<IReadOnlyList<McpToolSummary>>(
                (cursor, page, token) => ToolPageAsync(url, cursor, page, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new McpToolListing(
            McpToolReader.Merge(walk.Pages),
            new McpToolPaging(walk.Pages.Count, walk.Pages.Count > 0 ? walk.Pages[0].Count : 0, walk.Outcome));
    }

    /// <summary>
    /// Asks for one page of <c>tools/list</c>. The cursor goes in <c>params.cursor</c>, which is where the
    /// specification's pagination puts it, and is absent on the first request rather than sent as null.
    /// </summary>
    private async Task<McpResponse<CursorPage<IReadOnlyList<McpToolSummary>>>> ToolPageAsync(
        string url,
        string? cursor,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var parameters = McpJsonRpcChannel.Parameters();

        if (cursor is { Length: > 0 })
        {
            parameters["cursor"] = cursor;
        }

        var response = await _channel
            .CallAsync(url, "tools/list", $"skillforge-tools-{pageNumber}", parameters, cancellationToken)
            .ConfigureAwait(false);

        return response.Value is { } result
            ? McpResponse<CursorPage<IReadOnlyList<McpToolSummary>>>.Ok(
                new CursorPage<IReadOnlyList<McpToolSummary>>(
                    McpToolReader.Read(result["tools"]),
                    McpCursorWalk.NextCursor(result)))
            : McpResponse<CursorPage<IReadOnlyList<McpToolSummary>>>.Failed(response.Error ?? "no answer");
    }

    /// <summary>A whole tool list, with what it took to read it.</summary>
    private sealed record McpToolListing(IReadOnlyList<McpToolSummary> Tools, McpToolPaging Paging);

    private static string Body() =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "skillforge-discover-1",
            ["method"] = "server/discover",
            ["params"] = McpJsonRpcChannel.Parameters(),
        }.ToJsonString();

    private static McpServerProbe Read(string serverName, string payload)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return McpServerProbe.Failed(serverName, McpProbeStatus.Unreachable, "it answered something that is not JSON");
        }

        if (root?["error"] is { } error)
        {
            var code = error["code"]?.AsValue().TryGetValue<int>(out var parsed) is true ? parsed : 0;
            var message = error["message"]?.GetValue<string>() ?? "no message";

            return code == MethodNotFound
                ? McpServerProbe.Failed(serverName, McpProbeStatus.NoDiscovery, $"method not found: {message}")
                : McpServerProbe.Failed(serverName, McpProbeStatus.Unreachable, $"JSON-RPC error {code}: {message}");
        }

        if (root?["result"] is not { } result)
        {
            return McpServerProbe.Failed(
                serverName,
                McpProbeStatus.Unreachable,
                "it answered without a JSON-RPC result");
        }

        var serverInfo = result["_meta"]?[ServerInfoKey];

        return McpServerProbe.Answered(
            serverName,
            Strings(result["supportedVersions"]),
            Names(result["capabilities"]),
            serverInfo?["name"]?.GetValue<string>(),
            serverInfo?["version"]?.GetValue<string>(),
            Revision) with
        {
            Extensions = Extensions(result["capabilities"]?["extensions"]),
        };
    }

    /// <summary>
    /// Reads <c>capabilities.extensions</c>: an object keyed by extension identifier, each value an object of settings.
    /// </summary>
    /// <remarks>
    /// A setting counts as enabled only when it is the JSON literal <see langword="true"/>. <c>"true"</c> as a string
    /// is not a boolean, and reading it as one would make SkillForge call a method a server never offered.
    /// </remarks>
    private static IReadOnlyList<McpExtensionDeclaration> Extensions(JsonNode? node) =>
        node is JsonObject map
            ? [.. map
                .Select(entry => new McpExtensionDeclaration(
                    entry.Key,
                    entry.Value is JsonObject settings
                        ? [.. settings
                            .Where(setting => setting.Value is JsonValue value
                                && value.TryGetValue<bool>(out var enabled)
                                && enabled)
                            .Select(setting => setting.Key)
                            .Order(StringComparer.Ordinal)]
                        : []))
                .OrderBy(extension => extension.Identifier, StringComparer.Ordinal)]
            : [];

    private static IReadOnlyList<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? [.. array.OfType<JsonValue>()
                .Select(value => value.TryGetValue<string>(out var text) ? text : null)
                .OfType<string>()]
            : [];

    /// <summary>Capabilities are an object whose property names are the capabilities.</summary>
    private static IReadOnlyList<string> Names(JsonNode? node) =>
        node is JsonObject map
            ? [.. map.Select(entry => entry.Key).Order(StringComparer.Ordinal)]
            : [];

    private static Exception Innermost(Exception exception)
    {
        var current = exception;

        while (current.InnerException is { } inner)
        {
            current = inner;
        }

        return current;
    }
}
