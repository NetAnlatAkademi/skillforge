namespace SkillForge.Domain.Mcp;

/// <summary>
/// What a live MCP server said about itself.
/// </summary>
/// <param name="ServerName">The name the declaration gives the server, so the result can be matched back to it.</param>
/// <param name="Status">How the probe went.</param>
/// <param name="SupportedVersions">Protocol versions the server says it supports, ordered as it listed them.</param>
/// <param name="Capabilities">Capability names the server declares, ordered.</param>
/// <param name="SelfReportedName">
/// The <c>serverInfo.name</c> the server reports. The specification is explicit that this is self-reported, not
/// verified by the protocol, and that clients <strong>should not</strong> use it for security decisions — so it is
/// named "self-reported" everywhere it appears, including in the console output.
/// </param>
/// <param name="SelfReportedVersion">The <c>serverInfo.version</c> the server reports, under the same caveat.</param>
/// <param name="Detail">
/// Why a probe did not answer, in the underlying error's own words. <see langword="null"/> when it answered.
/// </param>
public sealed record McpServerProbe(
    string ServerName,
    McpProbeStatus Status,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> Capabilities,
    string? SelfReportedName,
    string? SelfReportedVersion,
    string? Detail)
{
    /// <summary>A server that answered.</summary>
    /// <param name="serverName">The declared name.</param>
    /// <param name="supportedVersions">Versions it listed.</param>
    /// <param name="capabilities">Capabilities it declared.</param>
    /// <param name="selfReportedName">Its self-reported name.</param>
    /// <param name="selfReportedVersion">Its self-reported version.</param>
    /// <returns>The probe result.</returns>
    public static McpServerProbe Answered(
        string serverName,
        IReadOnlyList<string> supportedVersions,
        IReadOnlyList<string> capabilities,
        string? selfReportedName,
        string? selfReportedVersion) =>
        new(
            serverName,
            McpProbeStatus.Answered,
            supportedVersions,
            capabilities,
            selfReportedName,
            selfReportedVersion,
            null);

    /// <summary>A server that could not be asked, or refused.</summary>
    /// <param name="serverName">The declared name.</param>
    /// <param name="status">Which kind of no-answer this was.</param>
    /// <param name="detail">The underlying reason.</param>
    /// <returns>The probe result.</returns>
    public static McpServerProbe Failed(string serverName, McpProbeStatus status, string detail) =>
        new(serverName, status, [], [], null, null, detail);
}
