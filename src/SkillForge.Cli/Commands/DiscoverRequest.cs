using SkillForge.Application.Abstractions;
using SkillForge.Domain.Discovery;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>discover</c> was asked to do.
/// </summary>
/// <remarks>
/// <see cref="Registry"/> has no default and never will. A discovery command that reaches a registry nobody named
/// makes a network request the user did not ask for, and the one place a security tool must not surprise anybody
/// is the network.
/// </remarks>
/// <param name="Query">What to search for.</param>
/// <param name="Registry">The registry URL to ask. Required.</param>
/// <param name="Kind">Which discovery adapter to use.</param>
/// <param name="Type">Restrict results to one resource type, or <see langword="null"/> for all of them.</param>
/// <param name="Limits">The bounds the request is made under.</param>
/// <param name="Format">Console or JSON. SARIF is not offered: a search result is not a set of findings.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record DiscoverRequest(
    string Query,
    string Registry,
    string Kind,
    DiscoveredResourceType? Type,
    RemoteDiscoveryLimits Limits,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
