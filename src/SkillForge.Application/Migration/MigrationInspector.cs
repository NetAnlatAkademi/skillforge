using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// Default <see cref="IMigrationInspector"/>: runs each adapter and merges the results in a fixed order.
/// </summary>
/// <remarks>
/// Sequential and ordered by provider identifier, so two runs on an unchanged machine produce the same report.
/// The inspector knows nothing about any provider's paths — that is the whole point of the adapters.
/// </remarks>
public sealed class MigrationInspector : IMigrationInspector
{
    private readonly IReadOnlyList<IAgentToolAdapter> _adapters;

    /// <summary>Initialises the inspector.</summary>
    /// <param name="adapters">One adapter per provider.</param>
    public MigrationInspector(IEnumerable<IAgentToolAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        _adapters = [.. adapters.OrderBy(adapter => adapter.ProviderId, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async Task<MigrationInspection> InspectAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providers = new List<AgentToolPresence>(_adapters.Count);
        var skills = new List<SkillInventoryEntry>();
        var mcpServers = new List<McpServerDeclaration>();
        var instructionFiles = new List<InstructionFileReference>();
        var diagnostics = new List<Diagnostic>();

        foreach (var adapter in _adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // No try/catch: an adapter that throws is a bug in that adapter, and hiding it would hand the user a
            // quietly incomplete inventory. A file it merely cannot parse is reported as SF1015 instead.
            var scan = await adapter.ScanAsync(request, cancellationToken).ConfigureAwait(false);

            providers.Add(scan.Presence);
            skills.AddRange(scan.Skills);
            mcpServers.AddRange(scan.McpServers);
            instructionFiles.AddRange(scan.InstructionFiles);
            diagnostics.AddRange(scan.Diagnostics);
        }

        return new MigrationInspection(
            request.UserDirectory,
            request.ProjectDirectory,
            providers,
            skills,
            mcpServers,
            instructionFiles,
            DiagnosticOrderingByCode(diagnostics));
    }

    /// <summary>
    /// Orders diagnostics by code and then by the file they name, which is enough here: the migration
    /// diagnostics all carry a path and none carries a line.
    /// </summary>
    private static IReadOnlyList<Diagnostic> DiagnosticOrderingByCode(IEnumerable<Diagnostic> diagnostics) =>
    [
        .. diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.FilePath, StringComparer.Ordinal),
    ];
}
