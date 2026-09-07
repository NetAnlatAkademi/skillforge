using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;

namespace SkillForge.Domain.Discovery;

/// <summary>
/// How far a declared resource got towards being checked against the thing it describes.
/// </summary>
/// <remarks>
/// **<see cref="VerifiedNoDrift"/> does not mean trusted, and it does not mean safe.** It means one narrow thing:
/// the capabilities a registry declared match the tools the server answered with, at the moment it was asked. A
/// server can match its listing exactly and still be malicious, still be compromised tomorrow, and still expose a
/// tool that deletes a database. Whether it is *permitted* is <c>policy check</c>'s answer; whether it is
/// *trustworthy* is not a question any tool answers by making one HTTP request.
///
/// The negative results are separated for the same reason the probe statuses are: "was not asked", "would not
/// answer" and "cannot be asked" call for different actions and must not look alike on a report.
/// </remarks>
public enum DiscoveryVerificationStatus
{
    /// <summary>Not asked. Verification is opt-in, per resource and per run.</summary>
    NotProbed = 0,

    /// <summary>
    /// Asked, answered, and everything comparable matched. Not a statement about trust or safety — see the
    /// remarks on this enum, and say so wherever this value is printed.
    /// </summary>
    VerifiedNoDrift,

    /// <summary>Asked, answered, and something comparable did not match.</summary>
    DriftDetected,

    /// <summary>Asked, and did not answer in a way anything could be compared against.</summary>
    ProbeFailed,

    /// <summary>
    /// Cannot be asked at all: not an MCP server, or one that is not reachable over HTTP. A stdio server lands
    /// here rather than being launched.
    /// </summary>
    UnsupportedResource,
}

/// <summary>
/// One kind of difference between what was declared and what a server actually answered.
/// </summary>
public enum DiscoveryDriftKind
{
    /// <summary>
    /// The server answered with a tool the listing did not declare. The high-value finding of the whole feature:
    /// it is the shape a capability nobody reviewed takes when it arrives.
    /// </summary>
    UnexpectedRuntimeTool = 0,

    /// <summary>The listing declared a tool the server does not have.</summary>
    DeclaredToolMissing,

    /// <summary>The number of tools differs, in a case where the names could not be compared one to one.</summary>
    ToolCountDrift,

    /// <summary>
    /// The version the registry names and the version the server reports about itself are not the same.
    /// </summary>
    /// <remarks>
    /// The server's half is <c>serverInfo.version</c>, which the specification states plainly is self-reported and
    /// not verified by the protocol. So this is a disagreement between two claims rather than a correction of one
    /// by the other, and it is reported in exactly those words.
    /// </remarks>
    SelfReportedVersionDrift,

    /// <summary>
    /// The registry's endpoint and the endpoint that actually answered are not the same.
    /// </summary>
    /// <remarks>
    /// **Not produced today**, and named here rather than left out so that the JSON contract does not have to
    /// grow a value later. Detecting it means knowing the URL that answered after redirects, and the MCP prober
    /// does not report one — it reports what a server said, not where the socket ended up. A drift kind that
    /// cannot be established would be an empty promise on a report, so nothing emits it and no diagnostic code
    /// has been published for it.
    /// </remarks>
    EndpointDrift,
}

/// <summary>
/// One difference, with where each side of it was read.
/// </summary>
/// <param name="Kind">Which kind of difference.</param>
/// <param name="Subject">
/// What the difference is about: a tool name, an endpoint. Named rather than counted, because a reviewer's next
/// action depends entirely on which tool appeared.
/// </param>
/// <param name="Declared">What the registry said, or <see langword="null"/> when it said nothing.</param>
/// <param name="Runtime">What the server answered, or <see langword="null"/> when it answered nothing.</param>
/// <param name="Evidence">
/// Where both sides were read — the registry URL and the endpoint that was probed. A finding that cannot say
/// where it read each half is asking to be trusted rather than checked.
/// </param>
public sealed record DiscoveryDrift(
    DiscoveryDriftKind Kind,
    string Subject,
    string? Declared,
    string? Runtime,
    DiscoveryEvidence Evidence);

/// <summary>
/// The two places a verification read from.
/// </summary>
/// <param name="Registry">The registry the declaration came from.</param>
/// <param name="Endpoint">The endpoint that was probed, or <see langword="null"/> when none was.</param>
/// <param name="ProbedRevision">
/// The MCP revision whose adapter got the answer, so a reader knows which dialect the runtime side is in.
/// </param>
public sealed record DiscoveryEvidence(string Registry, string? Endpoint, string? ProbedRevision);

/// <summary>
/// One resource, and what asking it about itself established.
/// </summary>
/// <param name="Resource">The resource as the registry declared it.</param>
/// <param name="Status">How far verification got.</param>
/// <param name="Detail">
/// Why a resource was not verified, in the underlying reason's own words. <see langword="null"/> when it was.
/// </param>
/// <param name="RuntimeTools">
/// The tools the server answered with, ordered by name. Empty when nothing was asked — which is not the same as a
/// server with no tools, and the status says which.
/// </param>
/// <param name="Paging">
/// What it took to read that tool list. An incomplete read makes every "missing declared tool" unreliable, and
/// the comparison suppresses those findings rather than reporting a tool as absent because reading stopped early.
/// </param>
/// <param name="Drifts">The differences found, ordered.</param>
public sealed record ResourceVerification(
    DiscoveredResource Resource,
    DiscoveryVerificationStatus Status,
    string? Detail,
    IReadOnlyList<McpToolSummary> RuntimeTools,
    McpToolPaging? Paging,
    IReadOnlyList<DiscoveryDrift> Drifts);

/// <summary>
/// What <c>discovery verify</c> found.
/// </summary>
/// <param name="Registry">The registry that was searched.</param>
/// <param name="Query">What was searched for.</param>
/// <param name="Resources">The resources, with what verification established about each, ordered by id.</param>
/// <param name="Diagnostics">
/// The findings, in the standard report order. These are drift findings and reading failures — never a verdict
/// about whether a resource should be used, which is a policy decision made elsewhere.
/// </param>
public sealed record DiscoveryVerificationReport(
    string Registry,
    string Query,
    IReadOnlyList<ResourceVerification> Resources,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Gets a value indicating whether anything drifted.</summary>
    public bool HasDrift => Resources.Any(resource => resource.Drifts.Count > 0);

    /// <summary>Gets how many resources were actually asked about themselves.</summary>
    public int ProbedCount =>
        Resources.Count(resource => resource.Status
            is DiscoveryVerificationStatus.VerifiedNoDrift
            or DiscoveryVerificationStatus.DriftDetected);
}
