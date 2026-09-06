using SkillForge.Application.Provenance;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// <see cref="IDistributionManifestReader"/> stub that answers per path.
/// </summary>
/// <remarks>
/// A path it was not told about fails to read, which is the case the inspector has to survive: a manifest that
/// cannot be parsed must be reported rather than silently turning a plugin into nothing.
/// </remarks>
internal sealed class FakeDistributionManifestReader : IDistributionManifestReader
{
    private readonly Dictionary<string, PluginManifest> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MarketplaceListing> _marketplaces = new(StringComparer.OrdinalIgnoreCase);

    internal FakeDistributionManifestReader WithPlugin(string path, PluginManifest manifest)
    {
        _plugins[Normalise(path)] = manifest;
        return this;
    }

    internal FakeDistributionManifestReader WithMarketplace(string path, MarketplaceListing listing)
    {
        _marketplaces[Normalise(path)] = listing;
        return this;
    }

    public Task<OperationResult<PluginManifest>> ReadPluginAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_plugins.TryGetValue(Normalise(path), out var manifest)
            ? OperationResult<PluginManifest>.Success(manifest)
            : OperationResult<PluginManifest>.Failure(Unreadable(path)));

    public Task<OperationResult<MarketplaceListing>> ReadMarketplaceAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_marketplaces.TryGetValue(Normalise(path), out var listing)
            ? OperationResult<MarketplaceListing>.Success(listing)
            : OperationResult<MarketplaceListing>.Failure(Unreadable(path)));

    private static Diagnostic Unreadable(string path) => Diagnostic.Warning(
        DiagnosticCodes.ProviderConfigurationNotParsable,
        $"'{path}' could not be read",
        path);

    private static string Normalise(string path) => path.Replace('\\', '/').TrimEnd('/');
}
