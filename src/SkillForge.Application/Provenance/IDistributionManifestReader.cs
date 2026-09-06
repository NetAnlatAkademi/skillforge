using SkillForge.Domain;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Reads the two files a plugin distribution writes down: what a plugin says it is, and what a marketplace says it
/// lists.
/// </summary>
/// <remarks>
/// A seam rather than a convenience. The file names and the JSON shape belong to somebody else's product and will
/// move; keeping the parsing behind an interface means a change there is one class in Infrastructure rather than a
/// change to what provenance means.
/// </remarks>
public interface IDistributionManifestReader
{
    /// <summary>Reads a plugin manifest.</summary>
    /// <param name="path">Path of the manifest file.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The manifest, or a failure carrying <c>SF1015</c> — a file that could not be read is reported rather than
    /// treated as a plugin that declares nothing.
    /// </returns>
    Task<OperationResult<PluginManifest>> ReadPluginAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Reads a marketplace file.</summary>
    /// <param name="path">Path of the marketplace file.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The listing, or a failure carrying <c>SF1015</c>.</returns>
    Task<OperationResult<MarketplaceListing>> ReadMarketplaceAsync(
        string path,
        CancellationToken cancellationToken = default);
}
