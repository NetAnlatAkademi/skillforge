using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Discovery;

/// <summary>
/// The bounds every remote discovery request is made under.
/// </summary>
/// <remarks>
/// **All of these exist because the other end is not ours.** A registry is a URL somebody typed on a command line,
/// and a URL somebody typed can answer slowly, answer enormously, answer with a redirect back to itself, or answer
/// with JSON nested a thousand deep. None of those is an attack that needs to be clever to work: a discovery
/// client without bounds hangs a CI job, and a hung CI job is indistinguishable from a broken one.
///
/// The defaults are deliberately modest. Discovery reads a search result; it is not a crawler and there is nothing
/// here it would be right to spend a minute on.
/// </remarks>
/// <param name="Timeout">How long one request may take, end to end.</param>
/// <param name="MaxResponseBytes">
/// How much of a response body will be read. A larger body is not truncated and parsed — a half-read JSON document
/// is a document that says something other than what was sent — it is refused, and the refusal is reported.
/// </param>
/// <param name="MaxRedirects">
/// How many redirects will be followed. Zero would break registries that legitimately redirect; unbounded lets one
/// point at itself.
/// </param>
/// <param name="MaxResults">
/// How many resources will be taken from one response. A registry that returns a hundred thousand listings has
/// answered a different question than the one that was asked, and the report says the result was truncated.
/// </param>
/// <param name="MaxJsonDepth">
/// How deeply nested a response may be. The parser's own recursion limit, set here rather than left at a default,
/// because a deeply nested document is the cheapest way to turn a parser into a stack overflow.
/// </param>
public sealed record RemoteDiscoveryLimits(
    TimeSpan Timeout,
    int MaxResponseBytes,
    int MaxRedirects,
    int MaxResults,
    int MaxJsonDepth)
{
    /// <summary>The bounds a run uses when nothing asks for others.</summary>
    public static RemoteDiscoveryLimits Default { get; } = new(
        TimeSpan.FromSeconds(20),
        MaxResponseBytes: 8 * 1024 * 1024,
        MaxRedirects: 3,
        MaxResults: 200,
        MaxJsonDepth: 32);
}

/// <summary>
/// One search of one registry.
/// </summary>
/// <param name="Registry">
/// The registry to ask. Required, always, and never defaulted: a discovery command that reaches a registry nobody
/// named is a command that makes a network request the user did not ask for.
/// </param>
/// <param name="Query">What to search for. Empty means "list what you have", where the registry supports it.</param>
/// <param name="Type">
/// Restrict results to one resource type, or <see langword="null"/> for all of them. Applied by SkillForge after
/// reading the response as well as passed to the registry, because a registry is not obliged to honour it.
/// </param>
/// <param name="Limits">The bounds the request is made under.</param>
public sealed record RemoteDiscoveryRequest(
    Uri Registry,
    string Query,
    DiscoveredResourceType? Type = null,
    RemoteDiscoveryLimits? Limits = null)
{
    /// <summary>Gets the bounds to apply, falling back to the defaults.</summary>
    public RemoteDiscoveryLimits EffectiveLimits => Limits ?? RemoteDiscoveryLimits.Default;
}

/// <summary>
/// What a registry answered.
/// </summary>
/// <param name="Registry">The registry that was asked.</param>
/// <param name="Kind">Which adapter asked it, so a report can say what dialect the answer was read as.</param>
/// <param name="Resources">The resources it listed, ordered by id.</param>
/// <param name="Truncated">
/// Whether more resources were listed than were read. A truncated result is a floor, and the report says so rather
/// than letting a count read as a total.
/// </param>
/// <param name="Diagnostics">
/// What could not be read, and why. A registry that did not answer, answered too much, or answered in a shape the
/// adapter does not recognise is reported here — never thrown, because a registry being unreachable is a fact
/// about that registry rather than a reason to abandon a run.
/// </param>
public sealed record RemoteDiscoveryResult(
    Uri Registry,
    string Kind,
    IReadOnlyList<DiscoveredResource> Resources,
    bool Truncated,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>A search that reached the registry and read nothing useful out of it.</summary>
    /// <param name="registry">The registry that was asked.</param>
    /// <param name="kind">The adapter that asked.</param>
    /// <param name="diagnostics">Why there is nothing.</param>
    /// <returns>The result.</returns>
    public static RemoteDiscoveryResult Nothing(
        Uri registry,
        string kind,
        params Diagnostic[] diagnostics) =>
        new(registry, kind, [], false, diagnostics);
}
