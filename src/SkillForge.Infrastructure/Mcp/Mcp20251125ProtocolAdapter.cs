using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// Asks an HTTP MCP server about itself using the handshake-based <c>initialize</c> of <c>2025-11-25</c> and earlier.
/// </summary>
/// <remarks>
/// The fallback for a server that answered <c>server/discover</c> with "method not found". Until this existed, such a
/// server was reported as SF8004 and nothing more; now the report says which revision it actually speaks and what it
/// declares.
///
/// **It sends <c>initialize</c> and stops.** The specification has the client follow a successful initialize with a
/// <c>notifications/initialized</c> notification before normal operations; SkillForge has no normal operations to begin,
/// so sending it would announce a session it is not going to use. Reading the response and leaving is the smaller
/// intrusion, and on HTTP there is no process left holding anything open.
///
/// Field names are the specification's own: request <c>params.protocolVersion</c>, <c>params.capabilities</c>,
/// <c>params.clientInfo</c>; result <c>protocolVersion</c>, <c>capabilities</c>, <c>serverInfo</c>. Note that the result
/// carries a single negotiated <c>protocolVersion</c> rather than the list <c>server/discover</c> returns — one of the
/// concrete reasons the newer revision is worth preferring.
/// </remarks>
public sealed class Mcp20251125ProtocolAdapter : IMcpProtocolAdapter
{
    private const string Revision = "2025-11-25";
    private const string ClientName = "skillforge";

    private static readonly string ClientVersion =
        typeof(Mcp20251125ProtocolAdapter).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private readonly HttpClient _client;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="client">The client to send with.</param>
    public Mcp20251125ProtocolAdapter(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
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
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");

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
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.RequiresAuthorization,
                    "it asked for authorization on initialize");
            }

            if (!response.IsSuccessStatusCode)
            {
                return McpServerProbe.Failed(
                    server.Name,
                    McpProbeStatus.Unreachable,
                    $"it answered {(int)response.StatusCode} {response.ReasonPhrase} to initialize");
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return Read(server.Name, payload);
        }
    }

    private static string Body() =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "skillforge-initialize-1",
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = Revision,

                // Empty: SkillForge offers a server nothing to call back into, and declaring roots or sampling it does
                // not implement would be a lie told to get a better answer.
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = ClientName,
                    ["version"] = ClientVersion,
                },
            },
        }.ToJsonString();

    private static McpServerProbe Read(string serverName, string payload)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(Json(payload));
        }
        catch (JsonException)
        {
            return McpServerProbe.Failed(
                serverName,
                McpProbeStatus.Unreachable,
                "it answered initialize with something that is not JSON");
        }

        if (root?["error"] is { } error)
        {
            return McpServerProbe.Failed(
                serverName,
                McpProbeStatus.Unreachable,
                $"initialize failed: {error["message"]?.GetValue<string>() ?? "no message"}");
        }

        if (root?["result"] is not { } result)
        {
            return McpServerProbe.Failed(
                serverName,
                McpProbeStatus.Unreachable,
                "it answered initialize without a JSON-RPC result");
        }

        var serverInfo = result["serverInfo"];
        var negotiated = result["protocolVersion"]?.GetValue<string>();

        return McpServerProbe.Answered(
            serverName,
            negotiated is null ? [] : [negotiated],
            Names(result["capabilities"]),
            serverInfo?["name"]?.GetValue<string>(),
            serverInfo?["version"]?.GetValue<string>(),
            Revision);
    }

    /// <summary>
    /// Unwraps a single server-sent event, because a 2025-11-25 Streamable HTTP server may answer a POST with an SSE
    /// stream rather than plain JSON. Only the first <c>data:</c> payload is read: that is the response to the one
    /// request this adapter sends, and nothing later in the stream is addressed to it.
    /// </summary>
    private static string Json(string payload)
    {
        if (!payload.StartsWith("event:", StringComparison.Ordinal)
            && !payload.StartsWith("data:", StringComparison.Ordinal))
        {
            return payload;
        }

        foreach (var line in payload.Split('\n'))
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return line["data:".Length..].Trim();
            }
        }

        return payload;
    }

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
