using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// The outcome of reading one configuration file.
/// </summary>
/// <param name="Servers">The servers declared, ordered by name so a report is reproducible.</param>
/// <param name="Diagnostics">
/// SF1015 when the file could not be read or parsed. Not a failure of the whole scan: the rest of the inventory is
/// still worth having, and a silent gap would look like a configuration that declares nothing.
/// </param>
public sealed record McpConfigurationReadResult(
    IReadOnlyList<McpServerDeclaration> Servers,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>A read that found no MCP section, which is not an error.</summary>
    public static McpConfigurationReadResult None { get; } = new([], []);

    /// <summary>Creates a successful result.</summary>
    /// <param name="servers">The servers declared.</param>
    /// <returns>The result.</returns>
    public static McpConfigurationReadResult Found(IReadOnlyList<McpServerDeclaration> servers) =>
        new(servers, []);

    /// <summary>Creates a result that reports a file it could not use.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="reason">Why it could not be used, in the underlying tool's own words.</param>
    /// <returns>The result.</returns>
    public static McpConfigurationReadResult Unreadable(string path, string reason) =>
        new([], [Diagnostic.Warning(
            DiagnosticCodes.ProviderConfigurationNotParsable,
            $"'{path}' could not be read, so the MCP servers it declares are missing from this inventory: {reason}",
            path,
            suggestion: "Check the file with the tool that owns it. SkillForge reports what it cannot read rather "
                + "than presenting an incomplete inventory as a complete one.")]);
}
