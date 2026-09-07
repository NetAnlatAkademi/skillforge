using SkillForge.Domain.Discovery;

namespace SkillForge.Application.Discovery;

/// <summary>
/// Asks one kind of remote registry what it lists.
/// </summary>
/// <remarks>
/// The same seam <see cref="Mcp.IMcpProtocolAdapter"/> is, for the same reason: discovery formats are younger than
/// MCP was when that abstraction was written, and Agentic Resource Discovery is a Proposal rather than a
/// specification. A version that moves a field, or a second registry with a different dialect, gets its own
/// implementation beside the first and nothing above them changes.
///
/// **Reading only, and never on its own initiative.** An implementation searches when it is asked, with the
/// registry it is given, under the bounds in the request. It does not install what it finds, connect to what it
/// finds, execute what it finds, cache anything, refresh anything in the background, or fall back to a registry
/// nobody named. Discovery that reached the network without being asked would be the one behaviour a security
/// tool cannot have.
/// </remarks>
public interface IRemoteResourceDiscoveryAdapter
{
    /// <summary>
    /// Gets what kind of registry this adapter speaks to, for example <c>ard</c> — the value a caller selects it
    /// by and a report names it by.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Searches one registry.
    /// </summary>
    /// <param name="request">Which registry, what to look for, and the bounds to do it under.</param>
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    /// <returns>
    /// What the registry listed, or why nothing could be read. This does not throw for a registry that is
    /// unreachable, slow, oversized or malformed: each of those is a fact about the registry, reported as a
    /// diagnostic alongside whatever was successfully read.
    /// </returns>
    ValueTask<RemoteDiscoveryResult> SearchAsync(
        RemoteDiscoveryRequest request,
        CancellationToken cancellationToken);
}
