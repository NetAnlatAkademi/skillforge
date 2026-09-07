using System.Text.Json.Nodes;
using SkillForge.Application.Discovery;
using SkillForge.Domain.Discovery;

namespace SkillForge.Infrastructure.Discovery;

/// <summary>
/// Reads an Agentic Resource Discovery registry.
/// </summary>
/// <remarks>
/// **ARD v0.91 is a Proposal, and this adapter is written to match that status honestly.** A proposal-stage format
/// moves: fields get renamed, containers get wrapped, JSON-LD contexts get rearranged. So the reader is
/// deliberately tolerant — it looks for a resource list under any of the containers such documents use, and reads
/// each field from any of the names it is commonly spelled with. That is not sloppiness dressed up as flexibility;
/// it is the difference between an adapter that survives the next revision and one that returns nothing the day
/// after the format changes.
///
/// The cost is stated plainly: **a field this reader does not recognise is preserved in
/// <see cref="DiscoveredResource.Metadata"/> and is never interpreted.** Nothing is guessed at, nothing unknown
/// is acted on, and no field is invented. When ARD stabilises, a strict reader for the stable version belongs
/// beside this one behind the same <see cref="IRemoteResourceDiscoveryAdapter"/> — which is what the abstraction
/// is for.
///
/// **The URL is used as given.** SkillForge does not invent a registry's query grammar: the query is appended as
/// <c>?q=</c> only when the supplied URL carries no query string of its own, and a URL that already has one is
/// sent verbatim. A registry that spells its search parameter differently is reached by putting the whole URL in
/// <c>--registry</c>, which is honest about what is known and what is not.
///
/// Nothing here connects to, installs or executes a discovered resource. The endpoint a listing names is read into
/// a <see cref="Uri"/> and left alone.
/// </remarks>
public sealed class ArdDiscoveryAdapter : IRemoteResourceDiscoveryAdapter
{
    /// <summary>The value <c>--kind</c> selects this adapter by.</summary>
    public const string AdapterKind = "ard";

    /// <summary>
    /// Property names a resource list is found under. <c>@graph</c> is JSON-LD's own; the rest are what
    /// search endpoints in this space actually return.
    /// </summary>
    private static readonly string[] ListProperties =
        ["@graph", "resources", "items", "results", "data", "agents", "servers", "entries"];

    private static readonly string[] IdProperties = ["@id", "id", "identifier", "urn"];
    private static readonly string[] NameProperties = ["name", "title", "label", "displayName"];
    private static readonly string[] TypeProperties = ["@type", "type", "resourceType", "kind", "category"];
    private static readonly string[] EndpointProperties =
        ["endpoint", "url", "uri", "serviceEndpoint", "remote", "href"];

    private static readonly string[] PublisherProperties =
        ["publisher", "author", "owner", "vendor", "maintainer", "organization"];

    private static readonly string[] VersionProperties = ["version", "revision", "schemaVersion"];
    private static readonly string[] CapabilityProperties = ["capabilities", "tools", "functions", "skills"];

    private readonly RegistryDocumentReader _reader;

    /// <summary>Initialises the adapter.</summary>
    /// <param name="client">
    /// The client to search with. Its handler owns the redirect bound; every other bound travels with the
    /// request and is applied by the shared <see cref="RegistryDocumentReader"/>.
    /// </param>
    public ArdDiscoveryAdapter(HttpClient client)
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
    /// The URL to ask. See the class remarks: a registry's own query grammar is not invented here.
    /// </summary>
    private static Uri SearchUri(RemoteDiscoveryRequest request)
    {
        if (request.Query.Length == 0 || request.Registry.Query.Length > 0)
        {
            return request.Registry;
        }

        return new UriBuilder(request.Registry) { Query = $"q={Uri.EscapeDataString(request.Query)}" }.Uri;
    }

    /// <summary>
    /// Finds the resource entries in whatever container the document wraps them in.
    /// </summary>
    /// <remarks>
    /// A bare array, a known list property, or — as a last resort — a single object that looks like one resource,
    /// which is what a registry returns when a URL addresses one listing rather than a search.
    /// </remarks>
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

        return document.Any(entry => IdProperties.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            ? [document]
            : [];
    }

    /// <summary>
    /// Maps one entry, or <see langword="null"/> when it has neither an id nor a name.
    /// </summary>
    /// <remarks>
    /// A resource that cannot be identified cannot be reported about, matched across two searches, or verified,
    /// and giving it a generated id would invent the one field a comparison depends on.
    /// </remarks>
    private static DiscoveredResource? ToResource(JsonObject entry, Uri registry)
    {
        var id = First(entry, IdProperties);
        var name = First(entry, NameProperties);

        if (id is null && name is null)
        {
            return null;
        }

        return new DiscoveredResource(
            id ?? name!,
            name ?? id!,
            TypeOf(entry),
            EndpointOf(entry),
            PublisherOf(entry),
            First(entry, VersionProperties),
            registry.ToString(),
            Capabilities(entry),
            Metadata(entry));
    }

    /// <summary>
    /// The resource type, matched loosely against whatever vocabulary the registry uses.
    /// </summary>
    /// <remarks>
    /// Substring matching, and <see cref="DiscoveredResourceType.Unknown"/> whenever nothing matches. A registry's
    /// <c>@type</c> may be a bare word, a JSON-LD IRI, or a list of both, and none of those is worth a
    /// version-specific parser while the format is a proposal. Unknown is the right answer for an unrecognised
    /// type: the only thing SkillForge does differently per type is decide what can be verified, and it will not
    /// probe something it could not classify.
    /// </remarks>
    private static DiscoveredResourceType TypeOf(JsonObject entry)
    {
        var declared = Values(entry, TypeProperties).ToArray();

        foreach (var value in declared)
        {
            if (Mentions(value, "mcp"))
            {
                return DiscoveredResourceType.McpServer;
            }

            if (Mentions(value, "skill"))
            {
                return DiscoveredResourceType.Skill;
            }

            if (Mentions(value, "workflow"))
            {
                return DiscoveredResourceType.Workflow;
            }

            if (Mentions(value, "agent"))
            {
                return DiscoveredResourceType.Agent;
            }

            if (Mentions(value, "api") || Mentions(value, "service"))
            {
                return DiscoveredResourceType.Api;
            }
        }

        return DiscoveredResourceType.Unknown;
    }

    private static bool Mentions(string value, string word) =>
        value.Contains(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The endpoint, when the listing names one that is an absolute URL.
    /// </summary>
    /// <remarks>
    /// Absolute, and only when the value itself declares a scheme. A relative endpoint would have to be resolved
    /// against a base the listing does not state, and a guessed base is a URL SkillForge would later print as
    /// though the registry had given it.
    /// </remarks>
    private static Uri? EndpointOf(JsonObject entry)
    {
        foreach (var value in Values(entry, EndpointProperties))
        {
            // AbsoluteUrl, not Uri.TryCreate: a schemeless path parses as file:// on Unix and not on Windows, and
            // an endpoint SkillForge synthesised is a value the registry never gave. It also strips any credential
            // the URL carried — see UrlRedaction.
            if (RegistryDocumentReader.AbsoluteUrl(value) is { } uri)
            {
                return uri;
            }
        }

        return null;
    }

    /// <summary>
    /// The publisher, from a string or from the <c>name</c> of an object — JSON-LD writes an organisation either
    /// way, and both are the same claim.
    /// </summary>
    private static string? PublisherOf(JsonObject entry)
    {
        foreach (var property in PublisherProperties)
        {
            switch (entry[property])
            {
                case JsonValue value when value.TryGetValue<string>(out var text) && text.Length > 0:
                    return text;

                case JsonObject nested when First(nested, NameProperties) is { } name:
                    return name;

                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>
    /// The declared capabilities, from an array of names, an array of objects, or an object whose keys are the
    /// names — all three of which appear in this space.
    /// </summary>
    private static IReadOnlyList<DeclaredCapability> Capabilities(JsonObject entry)
    {
        var capabilities = new List<DeclaredCapability>();

        foreach (var property in CapabilityProperties)
        {
            switch (entry[property])
            {
                case JsonArray array:
                    capabilities.AddRange(array.Select(item => Capability(item, property)).OfType<DeclaredCapability>());
                    break;

                case JsonObject map:
                    capabilities.AddRange(map
                        .Where(pair => pair.Key.Length > 0)
                        .Select(pair => new DeclaredCapability(pair.Key, KindOf(property), Text(pair.Value))));
                    break;

                default:
                    continue;
            }
        }

        return
        [
            .. capabilities
                .DistinctBy(capability => capability.Name, StringComparer.Ordinal)
                .OrderBy(capability => capability.Name, StringComparer.Ordinal),
        ];
    }

    private static DeclaredCapability? Capability(JsonNode? item, string property) => item switch
    {
        JsonValue value when value.TryGetValue<string>(out var name) && name.Length > 0 =>
            new DeclaredCapability(name, KindOf(property), null),

        JsonObject entry when First(entry, NameProperties) is { } name =>
            new DeclaredCapability(name, KindOf(property), First(entry, ["description", "summary", "doc"])),

        _ => null,
    };

    /// <summary>
    /// What sort of capability a property holds. Only <c>tools</c> says so unambiguously; <c>capabilities</c> is a
    /// container word and is left <see cref="DeclaredCapabilityKind.Unknown"/> rather than assumed to hold tools.
    /// </summary>
    private static DeclaredCapabilityKind KindOf(string property) =>
        property.Equals("tools", StringComparison.OrdinalIgnoreCase)
            ? DeclaredCapabilityKind.Tool
            : DeclaredCapabilityKind.Unknown;

    /// <summary>
    /// Every other scalar the listing carried, preserved as text and never interpreted — except that a value whose
    /// field name says it is a credential is withheld. See <see cref="SecretFieldNames"/>.
    /// </summary>
    /// <remarks>
    /// Extension namespaces included — <c>x-</c> prefixes, vendor keys, <c>_meta</c> members. A registry is free
    /// to add fields, and dropping the ones this reader does not know would remove exactly the evidence a person
    /// judging a listing would want. Preserved is not trusted: nothing reads these back to make a decision, and a
    /// value here has no more standing than the rest of the listing, which is to say none.
    ///
    /// Nested objects and arrays are not flattened. A report prints these, and a flattened tree printed as a flat
    /// list of keys reads as though the registry had sent it that way.
    /// </remarks>
    private static SortedDictionary<string, string> Metadata(JsonObject entry)
    {
        var known = new HashSet<string>(
            [
                .. IdProperties, .. NameProperties, .. TypeProperties, .. EndpointProperties,
                .. PublisherProperties, .. VersionProperties, .. CapabilityProperties,
            ],
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

        return metadata;
    }

    private static string? First(JsonObject entry, IReadOnlyList<string> properties)
    {
        foreach (var property in properties)
        {
            if (Text(entry[property]) is { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Every value a set of properties holds, flattening an array of strings into its members.</summary>
    private static IEnumerable<string> Values(JsonObject entry, IReadOnlyList<string> properties)
    {
        foreach (var property in properties)
        {
            switch (entry[property])
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        if (Text(item) is { Length: > 0 } member)
                        {
                            yield return member;
                        }
                    }

                    break;

                case { } node when Text(node) is { Length: > 0 } value:
                    yield return value;
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>A JSON scalar as text, read the same way for both discovery adapters.</summary>
    private static string? Text(JsonNode? node) => RegistryDocumentReader.Scalar(node);
}
