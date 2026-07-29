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
    private const string ProtocolVersionKey = "io.modelcontextprotocol/protocolVersion";
    private const string ClientInfoKey = "io.modelcontextprotocol/clientInfo";
    private const string ClientCapabilitiesKey = "io.modelcontextprotocol/clientCapabilities";
    private const string ServerInfoKey = "io.modelcontextprotocol/serverInfo";

    /// <summary>JSON-RPC's "method not found", which is how a server without discovery answers.</summary>
    private const int MethodNotFound = -32601;

    /// <summary>
    /// What SkillForge calls itself to a server. Not taken from the Reporting layer: Infrastructure may not reference
    /// it, and the dependency rules are worth more than sharing one string.
    /// </summary>
    private const string ClientName = "skillforge";

    private static readonly string ClientVersion =
        typeof(Mcp20260728ProtocolAdapter).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private readonly HttpClient _client;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="client">The client to send with. Timeouts and headers are the caller's to configure.</param>
    public Mcp20260728ProtocolAdapter(HttpClient client)
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

            // A second request, and only when the server said it has tools. Tool conformance cannot be checked without
            // the tool list, and the list cannot be had without asking — but a server that declares no tools is never
            // asked, and only the first page is read: paging through a large catalogue to inspect it is not proportionate.
            return probe.Status == McpProbeStatus.Answered
                && probe.Capabilities.Contains("tools", StringComparer.OrdinalIgnoreCase)
                    ? probe with { Tools = await ToolsAsync(url, cancellationToken).ConfigureAwait(false) }
                    : probe;
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
    /// Reads the first page of <c>tools/list</c>, reducing each tool to the facts a conformance check needs.
    /// </summary>
    /// <remarks>
    /// Failure here is deliberately quiet: the probe already succeeded, and a server that answers discovery but refuses
    /// its tool list has told us something less interesting than what we already have. An empty list is the honest
    /// result, and it produces no findings rather than false ones.
    /// </remarks>
    private async Task<IReadOnlyList<McpToolSummary>> ToolsAsync(string url, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "skillforge-tools-1",
            ["method"] = "tools/list",
            ["params"] = new JsonObject
            {
                ["_meta"] = new JsonObject
                {
                    [ProtocolVersionKey] = Revision,
                    [ClientInfoKey] = new JsonObject
                    {
                        ["name"] = ClientName,
                        ["version"] = ClientVersion,
                    },
                    [ClientCapabilitiesKey] = new JsonObject(),
                },
            },
        }.ToJsonString();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", Revision);

            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return McpToolReader.Read(JsonNode.Parse(payload)?["result"]?["tools"]);
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Body() =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "skillforge-discover-1",
            ["method"] = "server/discover",
            ["params"] = new JsonObject
            {
                ["_meta"] = new JsonObject
                {
                    [ProtocolVersionKey] = Revision,
                    [ClientInfoKey] = new JsonObject
                    {
                        ["name"] = ClientName,
                        ["version"] = ClientVersion,
                    },

                    // Empty on purpose: SkillForge is inspecting, not offering the server anything to call back into.
                    [ClientCapabilitiesKey] = new JsonObject(),
                },
            },
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
            Revision);
    }

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
