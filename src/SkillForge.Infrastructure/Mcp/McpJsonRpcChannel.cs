using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Domain.Mcp.Skills;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// Sends one JSON-RPC request to an HTTP MCP server in the <c>2026-07-28</c> dialect, under SkillForge's bounds.
/// </summary>
/// <remarks>
/// Every request after <c>server/discover</c> goes through here — <c>tools/list</c>, <c>skills/list</c>,
/// <c>skills/get</c>, <c>resources/read</c> and <c>resources/directory/read</c> — so there is one place that decides
/// what a request carries and how much of an answer is read. Two transports would eventually disagree about a bound.
///
/// Bounds: the client's timeout applies to the whole exchange, body included; the response body is read up to
/// <see cref="MaxResponseBytes"/> and refused past it, never truncated — half
/// a JSON document parses into something other than what was sent. JSON nesting is limited to
/// <see cref="MaxDepth"/>. The timeout is the <see cref="HttpClient"/>'s, and it is reported as an answer that did not
/// come rather than as the caller's cancellation.
///
/// It does not throw for a server that fails. It throws only when the caller cancels.
/// </remarks>
internal sealed class McpJsonRpcChannel
{
    /// <summary>The protocol revision this channel speaks.</summary>
    internal const string Revision = "2026-07-28";

    /// <summary>
    /// The largest response body read, in bytes: 24 MiB.
    /// </summary>
    /// <remarks>
    /// Sized for the largest answer a conforming server can owe a client: a skill file at the specification's 16 MiB
    /// per-skill limit, base64-encoded in a <c>blob</c> (a third larger), plus the JSON around it.
    /// </remarks>
    internal const int MaxResponseBytes = 24 * 1024 * 1024;

    /// <summary>The deepest JSON nesting accepted. Every shape this tool reads is less than ten levels deep.</summary>
    internal const int MaxDepth = 64;

    private const string ProtocolVersionKey = "io.modelcontextprotocol/protocolVersion";
    private const string ClientInfoKey = "io.modelcontextprotocol/clientInfo";
    private const string ClientCapabilitiesKey = "io.modelcontextprotocol/clientCapabilities";
    private const string ClientName = "skillforge";

    private static readonly string ClientVersion =
        typeof(McpJsonRpcChannel).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private static readonly JsonDocumentOptions ParseOptions = new() { MaxDepth = MaxDepth };

    private readonly HttpClient _client;

    /// <summary>Initialises the channel.</summary>
    /// <param name="client">The client to send with. Its timeout is the request timeout.</param>
    internal McpJsonRpcChannel(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// The <c>params</c> every request carries: the revision, who is asking, and an empty set of client capabilities —
    /// SkillForge is inspecting, not offering the server anything to call back into.
    /// </summary>
    /// <returns>A fresh parameters object the caller may add to.</returns>
    internal static JsonObject Parameters() =>
        new()
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
        };

    /// <summary>Sends one request and returns its <c>result</c>.</summary>
    /// <param name="url">The server's endpoint.</param>
    /// <param name="method">The JSON-RPC method.</param>
    /// <param name="id">The request id.</param>
    /// <param name="parameters">The <c>params</c> object, usually started from <see cref="Parameters"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The <c>result</c> node, or why there is none.</returns>
    internal async Task<McpResponse<JsonNode>> CallAsync(
        string url,
        string method,
        string id,
        JsonObject parameters,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        }.ToJsonString();

        // HttpClient.Timeout stops covering the exchange once headers arrive, because the body is streamed. One deadline
        // over the send and the body read is what keeps a server that drips its body from holding the run open.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_client.Timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", Revision);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Failed($"it answered {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            {
                return Failed($"it declared a response over the {MaxResponseBytes}-byte limit");
            }

            var payload = await BoundedBodyAsync(response, deadline.Token).ConfigureAwait(false);

            return payload is null
                ? Failed($"its response exceeded the {MaxResponseBytes}-byte limit")
                : Result(payload);
        }
        catch (HttpRequestException exception)
        {
            return Failed(Innermost(exception).Message);
        }
        catch (IOException exception)
        {
            // A connection reset mid-body: the body is streamed, so it surfaces here rather than inside SendAsync.
            return Failed(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            // A compressed body that does not decompress.
            return Failed(exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's own timeout, not the user's Ctrl+C: a cancelled run must still cancel.
            return Failed("it did not answer in time");
        }
    }

    private static McpResponse<JsonNode> Result(byte[] payload)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(payload, documentOptions: ParseOptions);
        }
        catch (JsonException)
        {
            return Failed("it answered something that is not JSON, or JSON nested too deeply");
        }

        if (root is not JsonObject envelope)
        {
            return Failed("it answered without a JSON-RPC envelope");
        }

        if (envelope["error"] is JsonObject error)
        {
            var code = error["code"] is JsonValue value && value.TryGetValue<int>(out var parsed) ? parsed : 0;
            var message = error["message"] is JsonValue text && text.TryGetValue<string>(out var said)
                ? said
                : "no message";

            return Failed($"JSON-RPC error {code.ToString(CultureInfo.InvariantCulture)}: {message}");
        }

        return envelope["result"] is { } result
            ? McpResponse<JsonNode>.Ok(result)
            : Failed("it answered without a JSON-RPC result");
    }

    /// <summary>
    /// Reads the body, or <see langword="null"/> when it is longer than <see cref="MaxResponseBytes"/>.
    /// </summary>
    /// <remarks>
    /// A growing buffer rather than one sized to the limit: most answers are a few kilobytes, and allocating 24 MiB
    /// per <c>tools/list</c> page to read one would be its own exhaustion.
    /// </remarks>
    private static async Task<byte[]?> BoundedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaxResponseBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static McpResponse<JsonNode> Failed(string error) => McpResponse<JsonNode>.Failed(error);

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
