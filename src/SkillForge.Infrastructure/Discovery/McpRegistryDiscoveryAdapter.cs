using System.Text.Json.Nodes;
using SkillForge.Application.Discovery;
using SkillForge.Domain.Discovery;

namespace SkillForge.Infrastructure.Discovery;

/// <summary>
/// Reads an MCP Registry listing.
/// </summary>
/// <remarks>
/// **Read-only, and that is a product decision rather than a missing feature.** This adapter searches and maps.
/// It does not publish, does not install, does not connect and does not execute. SkillForge is not a client for a
/// registry; it is the thing that checks whether a registry's claims survive contact with the servers behind them.
///
/// **Registry membership is provenance evidence, not trust.** Being listed in a registry — the official one
/// included — says where something was found and who put it there. It does not say the server is safe, that its
/// publisher is who they claim, or that its tool list is what the listing says. There is deliberately no field on
/// <see cref="DiscoveredResource"/> that could record otherwise, and the phrase "official registry means trusted"
/// appears nowhere in this codebase except as the assumption it forbids.
///
/// The difference from <see cref="ArdDiscoveryAdapter"/> is the document, not the posture. An MCP Registry listing
/// is a server entry: it names the server, where its source lives, and — for a remote server — one or more
/// endpoints under <c>remotes</c>. Every entry is an MCP server by construction, so the type is not guessed at.
/// Both adapters feed the same <see cref="DiscoveryVerifier"/>, which is the whole reason
/// <see cref="IRemoteResourceDiscoveryAdapter"/> exists.
/// </remarks>
public sealed class McpRegistryDiscoveryAdapter : IRemoteResourceDiscoveryAdapter
{
    /// <summary>The value <c>--kind</c> selects this adapter by.</summary>
    public const string AdapterKind = "mcp-registry";

    /// <summary>
    /// Property names a server list is found under. <c>servers</c> is the registry's own; the others are what a
    /// paginated or wrapped response uses.
    /// </summary>
    private static readonly string[] ListProperties = ["servers", "data", "results", "items"];

    /// <summary>
    /// Transport names that mean a server is reached over HTTP. Read from the entry rather than guessed at from
    /// the URL, because a listing that names a transport has said something and a URL has not.
    /// </summary>
    private static readonly string[] HttpTransports = ["streamable-http", "streamable_http", "http", "sse"];

    private readonly RegistryDocumentReader _reader;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="client">The client to search with.</param>
    public McpRegistryDiscoveryAdapter(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _reader = new RegistryDocumentReader(client);
    }

    /// <inheritdoc />
    public string Kind => AdapterKind;

    /// <inheritdoc />
    public async ValueTask<RemoteDiscoveryResult> SearchAsync(
        RemoteDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limits = request.EffectiveLimits;
        var document = await _reader
            .ReadAsync(SearchUri(request), limits, cancellationToken)
            .ConfigureAwait(false);

        if (document.Diagnostic is { } failure)
        {
            return RemoteDiscoveryResult.Nothing(request.Registry, AdapterKind, failure);
        }

        var resources = Entries(document.Document)
            .Select(entry => ToResource(entry, request.Registry))
            .OfType<DiscoveredResource>()

            // Applied even though every entry is an MCP server: a caller asking for a different type gets an
            // honest empty result rather than a list that quietly ignored the filter.
            .Where(resource => request.Type is null || resource.Type == request.Type)
            .DistinctBy(resource => resource.Id, StringComparer.Ordinal)
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .ToArray();

        var truncated = resources.Length > limits.MaxResults;

        return new RemoteDiscoveryResult(
            request.Registry,
            AdapterKind,
            truncated ? [.. resources.Take(limits.MaxResults)] : resources,
            truncated,
            truncated
                ? [RegistryDocumentReader.Truncated(request.Registry, resources.Length, limits.MaxResults)]
                : []);
    }

    /// <summary>
    /// The URL to ask.
    /// </summary>
    /// <remarks>
    /// The registry's search parameter is <c>search</c>, which is the one thing about its grammar this adapter
    /// takes as known — and it is still only added when the supplied URL carries no query of its own, so a URL
    /// somebody constructed by hand is sent exactly as they wrote it.
    /// </remarks>
    private static Uri SearchUri(RemoteDiscoveryRequest request)
    {
        if (request.Query.Length == 0 || request.Registry.Query.Length > 0)
        {
            return request.Registry;
        }

        return new UriBuilder(request.Registry)
        {
            Query = $"search={Uri.EscapeDataString(request.Query)}",
        }.Uri;
    }

    private static IEnumerable<JsonObject> Entries(JsonNode? root)
    {
        if (root is JsonArray array)
        {
            return array.OfType<JsonObject>();
        }

        if (root is not JsonObject document)
        {
            return [];
        }

        foreach (var property in ListProperties)
        {
            if (document[property] is JsonArray list)
            {
                return list.OfType<JsonObject>();
            }
        }

        // A single entry, which is what a URL addressing one server returns.
        return document["name"] is not null ? [document] : [];
    }

    /// <summary>
    /// Maps one server entry, or <see langword="null"/> when it does not name itself.
    /// </summary>
    /// <remarks>
    /// The registry identifies a server by its reverse-DNS name, so the name is the id: there is nothing more
    /// stable to key on, and generating one would invent the field a comparison across two searches depends on.
    /// </remarks>
    private static DiscoveredResource? ToResource(JsonObject entry, Uri registry)
    {
        if (Text(entry["name"]) is not { Length: > 0 } name)
        {
            return null;
        }

        return new DiscoveredResource(
            name,
            name,
            DiscoveredResourceType.McpServer,
            RemoteEndpoint(entry),
            Publisher(entry, name),
            Version(entry),
            registry.ToString(),

            // The registry does not list a server's tools, so there is nothing declared to compare names against.
            // An empty list here is what makes `discovery verify` report a tool-count observation rather than
            // calling every runtime tool unexpected — silence is not a claim.
            [],
            Metadata(entry));
    }

    /// <summary>
    /// The first remote endpoint the entry declares over an HTTP transport.
    /// </summary>
    /// <remarks>
    /// A server distributed only as a package has no endpoint, and it gets none here rather than one derived from
    /// its repository. Running a package to see what it exposes is the act SkillForge exists to let somebody
    /// defer, so a listing with only packages is reported as not verifiable and left alone.
    /// </remarks>
    private static Uri? RemoteEndpoint(JsonObject entry)
    {
        if (entry["remotes"] is not JsonArray remotes)
        {
            return null;
        }

        foreach (var remote in remotes.OfType<JsonObject>())
        {
            var transport = Text(remote["transport_type"]) ?? Text(remote["type"]);

            if (transport is not null
                && !HttpTransports.Contains(transport, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Uri.TryCreate(Text(remote["url"]), UriKind.Absolute, out var uri))
            {
                // A credential in the URL is removed where the URL is read — see UrlRedaction.
                return SkillForge.Domain.Mcp.UrlRedaction.WithoutCredentials(uri);
            }
        }

        return null;
    }

    /// <summary>
    /// The publisher, from the entry when it names one and from the server's own namespace otherwise.
    /// </summary>
    /// <remarks>
    /// The namespace fallback is the honest half of a claim the registry does verify: a reverse-DNS name's first
    /// segments are checked against domain or repository ownership before a server is published under them. So
    /// <c>io.github.acme/tools</c> says the publisher controls that GitHub account — which is a real fact, and
    /// still not a statement that the server is safe.
    /// </remarks>
    private static string? Publisher(JsonObject entry, string name)
    {
        if (Text(entry["publisher"]) is { Length: > 0 } declared)
        {
            return declared;
        }

        if (entry["repository"] is JsonObject repository
            && Text(repository["source"]) is { Length: > 0 } source
            && Text(repository["url"]) is { Length: > 0 } url)
        {
            return $"{source}: {url}";
        }

        var separator = name.LastIndexOf('/');

        return separator > 0 ? name[..separator] : null;
    }

    /// <summary>
    /// The version, from the entry or from the version detail object the registry nests it in.
    /// </summary>
    private static string? Version(JsonObject entry) =>
        Text(entry["version"])
        ?? (entry["version_detail"] as JsonObject is { } detail ? Text(detail["version"]) : null);

    /// <summary>
    /// The scalar fields of the entry, preserved as text and never interpreted — including a description, which
    /// is prose written by a publisher and reaches a report and nothing else.
    /// </summary>
    private static SortedDictionary<string, string> Metadata(JsonObject entry)
    {
        var known = new HashSet<string>(
            ["name", "publisher", "version", "version_detail", "remotes", "packages", "repository"],
            StringComparer.OrdinalIgnoreCase);

        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in entry)
        {
            if (known.Contains(key) || value is JsonObject or JsonArray)
            {
                continue;
            }

            // The name is kept and the value withheld when the name says it is a credential. Real listings
            // carry apiKey and authorization fields with the value in them, and every output prints metadata.
            if (SecretFieldNames.IsSecretName(key))
            {
                metadata[key] = SecretFieldNames.Withheld;
                continue;
            }

            if (RegistryDocumentReader.Scalar(value) is { } text)
            {
                metadata[key] = text;
            }
        }

        if (entry["packages"] is JsonArray packages)
        {
            // Named as a count, not resolved. How a package is distributed matters to whoever installs it, and
            // SkillForge installs nothing.
            metadata["packageCount"] = packages.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return metadata;
    }

    /// <summary>
    /// A JSON string, and only a string. An endpoint, a name and a transport that are not strings are not the
    /// thing they claim to be, and coercing a number into one would invent a URL.
    /// </summary>
    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
