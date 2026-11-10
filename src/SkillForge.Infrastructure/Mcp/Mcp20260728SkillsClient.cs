using System.Text;
using System.Text.Json.Nodes;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// The <c>io.modelcontextprotocol/skills</c> extension over HTTP, in the <c>2026-07-28</c> dialect it is defined
/// against.
/// </summary>
/// <remarks>
/// A thin reader on the shared <see cref="McpJsonRpcChannel"/> and <see cref="McpCursorWalk"/>: the request shape, the
/// response bound and the pagination guards are the ones <c>tools/list</c> already uses. This class only knows which
/// field of which result holds what. Field names are the specification's: <c>skills</c>, <c>skill</c>, <c>uri</c>,
/// <c>frontmatter</c>, <c>resources</c> (an array of <c>{uri, digest, size}</c> or the string <c>"dynamic"</c>),
/// <c>ttlMs</c>, <c>cacheScope</c>, <c>contents</c> and <c>nextCursor</c>.
///
/// It extracts and does not judge. A manifest with a bad digest comes back as written, and the Application layer
/// decides what that means.
/// </remarks>
public sealed class Mcp20260728SkillsClient : IMcpSkillsClient
{
    private const string Dynamic = "dynamic";
    private const string DirectoryMimeType = "inode/directory";

    /// <summary>
    /// Pages read from one directory. Far fewer than a tool list's hundred: a skill holds at most 512 files, so a
    /// directory that needs more than eight pages to list its children is not a skill directory, and the walk over a
    /// whole tree multiplies this number by every directory in it.
    /// </summary>
    private const int DirectoryPageBudget = 8;

    private readonly McpJsonRpcChannel _channel;

    /// <summary>Initialises the client.</summary>
    /// <param name="client">The client to send with. Its timeout is the per-request timeout.</param>
    public Mcp20260728SkillsClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _channel = new McpJsonRpcChannel(client);
    }

    /// <inheritdoc />
    public async Task<McpSkillListing> ListAsync(
        McpServerDeclaration server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.Command is not { Length: > 0 } url)
        {
            return new McpSkillListing([], 0, McpToolPagingOutcome.Complete, Detail: "the declaration names no URL");
        }

        var walk = await McpCursorWalk
            .WalkAsync<SkillPage>(
                async (cursor, page, token) =>
                {
                    var response = await _channel
                        .CallAsync(url, "skills/list", $"skillforge-skills-{page}", Paged(cursor), token)
                        .ConfigureAwait(false);

                    return response.Value is { } result
                        ? McpResponse<CursorPage<SkillPage>>.Ok(
                            new CursorPage<SkillPage>(ReadPage(result), McpCursorWalk.NextCursor(result)))
                        : McpResponse<CursorPage<SkillPage>>.Failed(response.Error ?? "no answer");
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var entries = walk.Pages.SelectMany(page => page.Entries).ToArray();
        var unique = entries
            .GroupBy(entry => entry.Uri, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.Uri, StringComparer.Ordinal)
            .ToArray();

        return new McpSkillListing(
            unique,
            walk.Pages.Count,
            walk.Outcome,
            walk.Pages.Sum(page => page.Malformed),
            entries.Length - unique.Length,
            walk.Error);
    }

    /// <inheritdoc />
    public async Task<McpResponse<McpSkillEntry>> GetAsync(
        McpServerDeclaration server,
        string skillUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrEmpty(skillUri);

        var response = await CallAsync(server, "skills/get", "skillforge-skill-get", skillUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.Value is not { } result)
        {
            return McpResponse<McpSkillEntry>.Failed(response.Error ?? "no answer");
        }

        return result["skill"] is JsonObject skill && Entry(skill, result) is { } entry
            ? McpResponse<McpSkillEntry>.Ok(entry)
            : McpResponse<McpSkillEntry>.Failed("the result carries no skill with a uri");
    }

    /// <inheritdoc />
    public async Task<McpResponse<McpResourceContent>> ReadAsync(
        McpServerDeclaration server,
        string resourceUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrEmpty(resourceUri);

        var response = await CallAsync(server, "resources/read", "skillforge-resource-read", resourceUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.Value is not { } result)
        {
            return McpResponse<McpResourceContent>.Failed(response.Error ?? "no answer");
        }

        // The item for the URI asked about; the first one when none names it, because a single-file answer often
        // leaves the uri off. Never another URI's item: that would verify one file's digest against another's bytes.
        var contents = result["contents"] as JsonArray ?? [];
        var item = contents.OfType<JsonObject>().FirstOrDefault(candidate =>
                string.Equals(Text(candidate["uri"]), resourceUri, StringComparison.Ordinal))
            ?? contents.OfType<JsonObject>().FirstOrDefault(candidate => Text(candidate["uri"]) is null);

        return item is null
            ? McpResponse<McpResourceContent>.Failed("the result carries no content for that uri")
            : Content(resourceUri, item);
    }

    /// <inheritdoc />
    public async Task<McpResponse<McpDirectoryListing>> ReadDirectoryAsync(
        McpServerDeclaration server,
        string directoryUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrEmpty(directoryUri);

        if (server.Command is not { Length: > 0 } url)
        {
            return McpResponse<McpDirectoryListing>.Failed("the declaration names no URL");
        }

        var walk = await McpCursorWalk
            .WalkAsync<IReadOnlyList<McpDirectoryEntry>>(
                async (cursor, page, token) =>
                {
                    var parameters = Paged(cursor);
                    parameters["uri"] = directoryUri;

                    var response = await _channel
                        .CallAsync(url, "resources/directory/read", $"skillforge-directory-{page}", parameters, token)
                        .ConfigureAwait(false);

                    return response.Value is { } result
                        ? McpResponse<CursorPage<IReadOnlyList<McpDirectoryEntry>>>.Ok(
                            new CursorPage<IReadOnlyList<McpDirectoryEntry>>(
                                Children(result["resources"]),
                                McpCursorWalk.NextCursor(result)))
                        : McpResponse<CursorPage<IReadOnlyList<McpDirectoryEntry>>>.Failed(
                            response.Error ?? "no answer");
                },
                DirectoryPageBudget,
                cancellationToken)
            .ConfigureAwait(false);

        if (walk.Pages.Count == 0)
        {
            return McpResponse<McpDirectoryListing>.Failed(walk.Error ?? "no answer");
        }

        return McpResponse<McpDirectoryListing>.Ok(new McpDirectoryListing(
            [.. walk.Pages
                .SelectMany(page => page)
                .DistinctBy(entry => entry.Uri, StringComparer.Ordinal)
                .OrderBy(entry => entry.Uri, StringComparer.Ordinal)],
            walk.Error is null ? walk.Outcome : McpToolPagingOutcome.PageLimitReached));
    }

    private async Task<McpResponse<JsonNode>> CallAsync(
        McpServerDeclaration server,
        string method,
        string id,
        string uri,
        CancellationToken cancellationToken)
    {
        if (server.Command is not { Length: > 0 } url)
        {
            return McpResponse<JsonNode>.Failed("the declaration names no URL");
        }

        var parameters = McpJsonRpcChannel.Parameters();
        parameters["uri"] = uri;

        return await _channel.CallAsync(url, method, id, parameters, cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject Paged(string? cursor)
    {
        var parameters = McpJsonRpcChannel.Parameters();

        if (cursor is { Length: > 0 })
        {
            parameters["cursor"] = cursor;
        }

        return parameters;
    }

    /// <summary>
    /// One page of <c>skills/list</c>. An entry without a uri is counted and skipped: it cannot be fetched, verified
    /// or named in a report, and one such entry must not take the rest of the page with it.
    /// </summary>
    private static SkillPage ReadPage(JsonNode result)
    {
        var raw = result["skills"] as JsonArray ?? [];
        var entries = raw.Select(node => node is JsonObject skill ? Entry(skill, result) : null).ToArray();

        return new SkillPage([.. entries.OfType<McpSkillEntry>()], entries.Count(entry => entry is null));
    }

    private static McpSkillEntry? Entry(JsonObject skill, JsonNode result)
    {
        if (Text(skill["uri"]) is not { Length: > 0 } uri)
        {
            return null;
        }

        return new McpSkillEntry(
            uri,
            skill["frontmatter"] is JsonObject frontmatter ? frontmatter.ToJsonString() : null,
            Manifest(skill["resources"]),
            result["ttlMs"] is JsonValue ttl && ttl.TryGetValue<long>(out var milliseconds) ? milliseconds : null,
            Text(result["cacheScope"]));
    }

    private static McpSkillManifest Manifest(JsonNode? resources) => resources switch
    {
        null => McpSkillManifest.Invalid("resources is missing"),
        JsonValue value when Text(value) == Dynamic => McpSkillManifest.Dynamic,
        JsonArray array when array.All(item => item is JsonObject) =>
            McpSkillManifest.Static([.. array.OfType<JsonObject>().Select(Resource)]),
        JsonArray => McpSkillManifest.Invalid("a resources entry is not an object"),
        _ => McpSkillManifest.Invalid($"resources is {resources.GetValueKind().ToString().ToLowerInvariant()}, "
            + "neither an array nor \"dynamic\""),
    };

    /// <summary>
    /// One manifest file. A size that is not a whole number is left null rather than rounded: a declared size that
    /// cannot be compared exactly is not a declared size.
    /// </summary>
    private static McpSkillResource Resource(JsonObject resource) =>
        new(
            Text(resource["uri"]) ?? string.Empty,
            Text(resource["digest"]),
            resource["size"] is JsonValue size && size.TryGetValue<long>(out var bytes) ? bytes : null);

    /// <summary>
    /// The raw bytes of one <c>contents</c> item: <c>text</c> as UTF-8, <c>blob</c> from base64.
    /// </summary>
    /// <remarks>
    /// A <c>text</c> item has already been decoded by the server, so its bytes are re-encoded as UTF-8 — which is what
    /// the digest is computed over for any file the server stored as UTF-8. A file stored in another encoding, or with
    /// a byte order mark, does not survive that round trip and will be reported as a mismatch; that limit is stated in
    /// <c>docs/mcp-skills.md</c>.
    /// </remarks>
    private static McpResponse<McpResourceContent> Content(string uri, JsonObject item)
    {
        var mimeType = Text(item["mimeType"]);

        if (Text(item["text"]) is { } text)
        {
            return McpResponse<McpResourceContent>.Ok(new McpResourceContent(uri, Encoding.UTF8.GetBytes(text), mimeType));
        }

        if (Text(item["blob"]) is not { } blob)
        {
            return McpResponse<McpResourceContent>.Failed("the content carries neither text nor blob");
        }

        try
        {
            return McpResponse<McpResourceContent>.Ok(new McpResourceContent(uri, Convert.FromBase64String(blob), mimeType));
        }
        catch (FormatException)
        {
            return McpResponse<McpResourceContent>.Failed("the blob is not valid base64");
        }
    }

    private static IReadOnlyList<McpDirectoryEntry> Children(JsonNode? resources) =>
        resources is JsonArray array
            ? [.. array
                .OfType<JsonObject>()
                .Select(child => (Uri: Text(child["uri"]), MimeType: Text(child["mimeType"])))
                .Where(child => child.Uri is { Length: > 0 })
                .Select(child => new McpDirectoryEntry(
                    child.Uri!,
                    string.Equals(child.MimeType, DirectoryMimeType, StringComparison.Ordinal)))]
            : [];

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>One page of <c>skills/list</c>: the entries it carried, and how many it had to skip.</summary>
    private sealed record SkillPage(IReadOnlyList<McpSkillEntry> Entries, int Malformed);
}
