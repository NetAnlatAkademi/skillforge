namespace SkillForge.Domain.Discovery;

/// <summary>
/// What kind of thing a registry says it is listing.
/// </summary>
/// <remarks>
/// Provider-neutral, and short on purpose. A registry's own vocabulary is richer than this and is preserved
/// verbatim in <see cref="DiscoveredResource.Metadata"/>; what the core needs is the handful of distinctions that
/// change what SkillForge can do next — and today only <see cref="McpServer"/> can be verified against a runtime.
/// </remarks>
public enum DiscoveredResourceType
{
    /// <summary>Nothing in the listing said what it is, or it said something nothing here recognises.</summary>
    Unknown = 0,

    /// <summary>An agent skill.</summary>
    Skill,

    /// <summary>An MCP server.</summary>
    McpServer,

    /// <summary>An agent.</summary>
    Agent,

    /// <summary>A multi-step workflow.</summary>
    Workflow,

    /// <summary>A plain HTTP API.</summary>
    Api,
}

/// <summary>
/// What sort of thing a declared capability is.
/// </summary>
public enum DeclaredCapabilityKind
{
    /// <summary>The listing named a capability without saying what sort it is.</summary>
    Unknown = 0,

    /// <summary>A callable tool.</summary>
    Tool,

    /// <summary>A readable resource.</summary>
    Resource,

    /// <summary>A prompt template.</summary>
    Prompt,
}

/// <summary>
/// One capability a registry says a resource has.
/// </summary>
/// <remarks>
/// **Declared, not observed.** This is a registry's claim, written down by whoever published the listing, and
/// nothing about it has been checked against a running server. That distinction is the whole reason this type is
/// separate from <see cref="Mcp.McpToolSummary"/>, which is what a server itself answered.
/// </remarks>
/// <param name="Name">The capability's name as the listing gives it.</param>
/// <param name="Kind">What sort of capability the listing says it is.</param>
/// <param name="Description">A description the listing carries, or <see langword="null"/>.</param>
public sealed record DeclaredCapability(string Name, DeclaredCapabilityKind Kind, string? Description);

/// <summary>
/// One resource a registry returned.
/// </summary>
/// <remarks>
/// **Everything in this type is a claim made by a registry.** The name, the publisher, the endpoint, the
/// capabilities and every entry in <see cref="Metadata"/> were written by whoever published the listing, and
/// SkillForge has verified none of them. Membership of a registry — any registry, official or otherwise — is
/// evidence about where something was found and is not evidence that it is safe, trusted or what it says it is.
///
/// That is why this record has no trust field and no score. Whether a resource is acceptable is decided by
/// <c>policy check</c>; whether it is what it claims is decided by <c>discovery verify</c>, which asks the server
/// itself. Putting a verdict here would let a listing's own metadata answer a question only those layers can.
///
/// The type is also deliberately free of any registry's schema. There is no JSON-LD in it, no <c>@context</c> and
/// no version-specific field: a discovery format at proposal stage will change, and when it does the change should
/// land in one adapter rather than in everything that reads a resource.
/// </remarks>
/// <param name="Id">The identifier the registry gives the resource, used to match it across two searches.</param>
/// <param name="Name">The resource's name as the registry gives it.</param>
/// <param name="Type">What the registry says it is.</param>
/// <param name="Endpoint">
/// Where the registry says it can be reached, or <see langword="null"/> when the listing names nowhere. Never
/// connected to by discovery itself.
/// </param>
/// <param name="Publisher">Who the registry says published it.</param>
/// <param name="Version">The version the registry names.</param>
/// <param name="SourceRegistry">
/// The registry this came from. Kept on every resource so that a report can say where a claim was read even when
/// two registries are searched in one run.
/// </param>
/// <param name="Capabilities">The capabilities the registry says it has, ordered by name.</param>
/// <param name="Metadata">
/// Every other field the listing carried, as text, ordered by key. Extension namespaces included: a registry is
/// free to add fields, and dropping them would lose the evidence a reader needs to judge the listing. Preserved is
/// not the same as trusted — nothing in here is ever acted on.
/// </param>
public sealed record DiscoveredResource(
    string Id,
    string Name,
    DiscoveredResourceType Type,
    Uri? Endpoint,
    string? Publisher,
    string? Version,
    string SourceRegistry,
    IReadOnlyList<DeclaredCapability> Capabilities,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>Gets the names of the capabilities the registry declared, ordered.</summary>
    public IReadOnlyList<string> DeclaredCapabilityNames =>
    [
        .. Capabilities
            .Select(capability => capability.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Gets a value indicating whether this resource is one <c>discovery verify</c> can ask about itself.
    /// </summary>
    /// <remarks>
    /// An MCP server at an <c>http</c> or <c>https</c> endpoint, and nothing else. A stdio server is never
    /// launched, and a skill, an agent or a workflow cannot be "asked" anything without running it — which is the
    /// act SkillForge exists to let somebody defer.
    /// </remarks>
    public bool IsRemotelyVerifiable =>
        Type == DiscoveredResourceType.McpServer
        && Endpoint is { } endpoint
        && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);
}
