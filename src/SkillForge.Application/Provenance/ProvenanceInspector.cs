using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Works out where each asset under a directory came from.
/// </summary>
/// <remarks>
/// Every value it reports was read from somewhere: a plugin manifest, a marketplace file, the git checkout, or the
/// bytes on disk. Nothing is inferred from a name, a shape or a resemblance — an upstream is reported only when a
/// manifest names one, because "this skill looks like the one in that popular repository" is a guess, and a guess
/// in a provenance report is read as a finding.
///
/// It describes and does not judge. Unknown provenance is an ordinary result and exits zero; whether an
/// organisation accepts it is a policy decision, and policy is where SkillForge makes those.
/// </remarks>
public sealed class ProvenanceInspector
{
    /// <summary>Directory a plugin keeps its manifest in.</summary>
    public const string PluginMetadataDirectory = ".claude-plugin";

    /// <summary>File a plugin describes itself in.</summary>
    public const string PluginManifestFileName = "plugin.json";

    /// <summary>File a marketplace lists its plugins in.</summary>
    public const string MarketplaceFileName = "marketplace.json";

    /// <summary>Directories never walked into, matching <c>SkillDiscovery</c>.</summary>
    private static readonly string[] IgnoredDirectoryNames =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    private readonly IFileSystem _fileSystem;
    private readonly ISkillDiscovery _skillDiscovery;
    private readonly ISkillLoader _skillLoader;
    private readonly IRepositoryFactsReader _repositoryFacts;
    private readonly IDistributionManifestReader _manifests;
    private readonly AssetFingerprinter _fingerprinter;

    /// <summary>Initialises the inspector.</summary>
    /// <param name="fileSystem">Walks the tree.</param>
    /// <param name="skillDiscovery">Finds the skills.</param>
    /// <param name="skillLoader">Reads each skill's own declarations.</param>
    /// <param name="repositoryFacts">Asks git where the checkout came from and what is uncommitted.</param>
    /// <param name="manifests">Reads plugin and marketplace files.</param>
    /// <param name="fingerprinter">Hashes each asset's contents.</param>
    public ProvenanceInspector(
        IFileSystem fileSystem,
        ISkillDiscovery skillDiscovery,
        ISkillLoader skillLoader,
        IRepositoryFactsReader repositoryFacts,
        IDistributionManifestReader manifests,
        AssetFingerprinter fingerprinter)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(skillDiscovery);
        ArgumentNullException.ThrowIfNull(skillLoader);
        ArgumentNullException.ThrowIfNull(repositoryFacts);
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        _fileSystem = fileSystem;
        _skillDiscovery = skillDiscovery;
        _skillLoader = skillLoader;
        _repositoryFacts = repositoryFacts;
        _manifests = manifests;
        _fingerprinter = fingerprinter;
    }

    /// <summary>Reads the provenance of every asset under a directory.</summary>
    /// <param name="path">Directory to scan.</param>
    /// <param name="includeLocalModifications">
    /// Whether to ask git what is uncommitted, per asset. One process per asset, which is worth it for a report
    /// about a repository and is not worth it for a summary over an installed machine — so a caller that only
    /// wants the counts can turn it off, and the assets then report zero modifications rather than a guess.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>What was found. Assets are ordered by path so two runs can be compared.</returns>
    public async Task<ProvenanceReport> InspectAsync(
        string path,
        bool includeLocalModifications = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = _fileSystem.GetFullPath(path);
        var facts = await _repositoryFacts.ReadFactsAsync(root, cancellationToken).ConfigureAwait(false);

        var findings = new List<Diagnostic>();
        var marketplaces = await ReadMarketplacesAsync(root, findings, cancellationToken).ConfigureAwait(false);

        var assets = new List<AssetProvenance>();

        // Plugins first, and their sources kept by directory: a skill inside a plugin is distributed by that
        // plugin, and it can only inherit what has already been read.
        var plugins = new Dictionary<string, DistributionSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in FindFiles(root, PluginManifestFileName))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var asset = await ReadPluginAsync(
                    root,
                    facts,
                    marketplaces,
                    manifestPath,
                    findings,
                    includeLocalModifications,
                    cancellationToken)
                .ConfigureAwait(false);

            if (asset is not null)
            {
                assets.Add(asset);
                plugins[PluginDirectoryOf(manifestPath)] = asset.Source;
            }
        }

        foreach (var skillDirectory in _skillDiscovery.FindSkillDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            assets.Add(await ReadSkillAsync(
                    root,
                    facts,
                    plugins,
                    skillDirectory,
                    includeLocalModifications,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        return new ProvenanceReport(
            root,
            facts.Repository,
            facts.Commit,
            [.. assets.OrderBy(asset => asset.Path, StringComparer.Ordinal)],
            [.. findings]);
    }

    /// <summary>
    /// Reads every marketplace file in the tree, so a plugin can be matched to the listing that distributes it.
    /// </summary>
    private async Task<IReadOnlyList<MarketplaceListing>> ReadMarketplacesAsync(
        string root,
        List<Diagnostic> findings,
        CancellationToken cancellationToken)
    {
        var listings = new List<MarketplaceListing>();

        foreach (var file in FindFiles(root, MarketplaceFileName))
        {
            var result = await _manifests.ReadMarketplaceAsync(file, cancellationToken).ConfigureAwait(false);

            findings.AddRange(result.Diagnostics);

            if (result.Value is { } listing)
            {
                listings.Add(listing);
            }
        }

        return listings;
    }

    private async Task<AssetProvenance?> ReadPluginAsync(
        string root,
        RepositoryFacts facts,
        IReadOnlyList<MarketplaceListing> marketplaces,
        string manifestPath,
        List<Diagnostic> findings,
        bool includeLocalModifications,
        CancellationToken cancellationToken)
    {
        var result = await _manifests.ReadPluginAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        findings.AddRange(result.Diagnostics);

        if (result.Value is not { } manifest)
        {
            return null;
        }

        var directory = PluginDirectoryOf(manifestPath);
        var listing = marketplaces.FirstOrDefault(candidate => candidate.Entries.Any(entry =>
            string.Equals(entry.PluginName, manifest.Name, StringComparison.OrdinalIgnoreCase)));

        var entry = listing?.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.PluginName, manifest.Name, StringComparison.OrdinalIgnoreCase));

        var source = new DistributionSource(
            listing?.Name,
            manifest.Publisher ?? listing?.Publisher,
            manifest.RepositoryUrl ?? entry?.RepositoryUrl ?? facts.Repository,
            manifest.Version ?? entry?.Version,
            facts.Commit,
            await _fingerprinter.ComputeAsync(directory, cancellationToken).ConfigureAwait(false),
            UpdateModeInference.From(entry));

        var evidence = new List<string> { Relative(root, manifestPath) };
        if (listing is not null)
        {
            evidence.Add(Relative(root, listing.Path));
        }

        // The upstream is the manifest's own claim, and only counts as one when it differs from where the plugin
        // is distributed from. A manifest pointing at its own repository has declared no fork.
        var upstream = manifest.RepositoryUrl is { Length: > 0 } declared
            && entry?.RepositoryUrl is { Length: > 0 } distributed
            && !string.Equals(declared, distributed, StringComparison.OrdinalIgnoreCase)
                ? declared
                : null;

        return await BuildAsync(
            root,
            facts,
            manifest.Name,
            AssetKind.Plugin,
            directory,
            source,
            upstream,
            evidence,
            null,
            includeLocalModifications,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssetProvenance> ReadSkillAsync(
        string root,
        RepositoryFacts facts,
        IReadOnlyDictionary<string, DistributionSource> plugins,
        string directory,
        bool includeLocalModifications,
        CancellationToken cancellationToken)
    {
        var load = await _skillLoader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);
        var name = load.Value is { Name.Length: > 0 } loaded
            ? loaded.Name
            : Path.GetFileName(directory.TrimEnd('/', '\\'));

        // A skill inside a plugin is distributed by that plugin, so it inherits the plugin's marketplace,
        // publisher and update mode rather than reporting none: what arrives in the plugin's next version arrives
        // here too. Its fingerprint stays its own, because the point of listing it separately is that a skill can
        // be edited without the plugin's version changing.
        var container = plugins.Keys.FirstOrDefault(plugin => IsInside(plugin, directory));
        var inherited = container is null ? null : plugins[container];

        var source = new DistributionSource(
            inherited?.Marketplace,
            inherited?.Publisher,
            inherited?.RepositoryUrl ?? facts.Repository,
            load.Value?.Frontmatter.Version,
            facts.Commit,
            await _fingerprinter.ComputeAsync(directory, cancellationToken).ConfigureAwait(false),
            inherited?.UpdateMode ?? UpdateMode.Unknown);

        var evidence = new List<string>
        {
            Relative(root, Path.Combine(directory, SkillDefinition.SkillFileName)),
        };

        if (container is not null)
        {
            evidence.Add(Relative(root, container));
        }

        return await BuildAsync(
            root,
            facts,
            name,
            AssetKind.Skill,
            directory,
            source,
            null,
            evidence,
            container is null ? null : Relative(facts.Root ?? root, container),
            includeLocalModifications,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fills in the parts every asset shares: what git says about it, and what that adds up to.</summary>
    private async Task<AssetProvenance> BuildAsync(
        string root,
        RepositoryFacts facts,
        string name,
        AssetKind kind,
        string directory,
        DistributionSource source,
        string? upstream,
        List<string> evidence,
        string? distributedBy,
        bool includeLocalModifications,
        CancellationToken cancellationToken)
    {
        var modified = 0;

        if (facts.IsRepository && includeLocalModifications)
        {
            var status = await _repositoryFacts
                .ReadStatusAsync(directory, directory, cancellationToken)
                .ConfigureAwait(false);

            modified = status.ModifiedPaths.Count;
            evidence.Add("git status --porcelain");
        }

        return new AssetProvenance(
            name,
            kind,
            Relative(facts.Root ?? root, directory),
            source,
            StatusOf(source, upstream, modified),
            upstream,
            modified,
            [.. evidence],
            distributedBy);
    }

    /// <summary>
    /// Decides the one status that best describes an asset.
    /// </summary>
    /// <remarks>
    /// Ordered by what a reviewer most needs to know. A modified working copy comes first because it makes every
    /// other answer conditional — the commit is named, but it is not what is on disk. <c>Drifted</c>,
    /// <c>PublisherChanged</c> and <c>MarketplaceChanged</c> are never produced here: a single snapshot has nothing
    /// to have drifted from, and <c>provenance diff</c> is what compares two.
    /// </remarks>
    private static ProvenanceStatus StatusOf(DistributionSource source, string? upstream, int modifiedFiles)
    {
        if (modifiedFiles > 0)
        {
            return ProvenanceStatus.Modified;
        }

        if (upstream is { Length: > 0 })
        {
            return ProvenanceStatus.Forked;
        }

        if (source.UpdateMode is UpdateMode.Automatic or UpdateMode.Floating)
        {
            return ProvenanceStatus.Unpinned;
        }

        return source.IdentifiesItsSource ? ProvenanceStatus.VerifiedSource : ProvenanceStatus.Unknown;
    }

    /// <summary>Finds files by name under the metadata directory, skipping the trees nothing is read from.</summary>
    private IEnumerable<string> FindFiles(string root, string fileName)
    {
        if (!_fileSystem.DirectoryExists(root))
        {
            return [];
        }

        return _fileSystem.EnumerateFiles(root)
            .Where(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
            .Where(file => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(file)),
                PluginMetadataDirectory,
                StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsIgnored(root, file))
            .Order(StringComparer.Ordinal);
    }

    private static bool IsIgnored(string root, string file) =>
        Path.GetRelativePath(root, file)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));

    /// <summary>The plugin directory is the one above <c>.claude-plugin</c>, not the metadata directory.</summary>
    private static string PluginDirectoryOf(string manifestPath) =>
        Path.GetDirectoryName(Path.GetDirectoryName(manifestPath)!)!;

    private static bool IsInside(string outer, string candidate)
    {
        var prefix = outer.Replace('\\', '/').TrimEnd('/') + '/';

        return candidate.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Expresses a path relative to the repository root, with forward slashes. An absolute path from a build agent
    /// means nothing to whoever reads the report later, and a path relative to the repository is the one both sides
    /// of a diff can agree on.
    /// </summary>
    private static string Relative(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        return relative.Length == 0 ? "." : relative;
    }
}
