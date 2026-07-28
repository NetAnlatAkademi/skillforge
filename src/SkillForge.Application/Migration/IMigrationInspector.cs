using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// Reports what agent tooling is installed and how it is configured.
/// </summary>
public interface IMigrationInspector
{
    /// <summary>
    /// Runs every adapter and merges what they found.
    /// </summary>
    /// <param name="request">Where to look.</param>
    /// <param name="cancellationToken">Token used to cancel the inspection.</param>
    /// <returns>The inventory, including the providers that were not found.</returns>
    Task<MigrationInspection> InspectAsync(
        AgentToolScanRequest request,
        CancellationToken cancellationToken = default);
}
