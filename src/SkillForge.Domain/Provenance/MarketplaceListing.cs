namespace SkillForge.Domain.Provenance;

/// <summary>
/// A marketplace file, and the plugins it lists.
/// </summary>
/// <param name="Name">The marketplace's declared name, or the file's directory name when it declares none.</param>
/// <param name="Publisher">Owner as the file names them.</param>
/// <param name="Path">The file this was read from.</param>
/// <param name="Entries">The plugins it lists, in the order they were read.</param>
public sealed record MarketplaceListing(
    string Name,
    string? Publisher,
    string Path,
    IReadOnlyList<MarketplaceEntry> Entries);

/// <summary>
/// One plugin as a marketplace lists it.
/// </summary>
/// <remarks>
/// <paramref name="DeclaredUpdateMode"/> and <paramref name="Revision"/> are kept apart from the
/// <see cref="UpdateMode"/> they imply, because the mapping between them is a decision and this is a reading. The
/// decision lives in <c>UpdateModeInference</c>, where it can be argued with without a JSON file.
/// </remarks>
/// <param name="PluginName">Name of the plugin the entry lists.</param>
/// <param name="RepositoryUrl">Where the entry says the plugin comes from.</param>
/// <param name="Revision">The revision the entry names: a commit, a tag, a branch, or nothing.</param>
/// <param name="Version">The version the entry names.</param>
/// <param name="AutomaticUpdate">
/// Whether the entry declares that the plugin updates itself. <see langword="null"/> when it says nothing, which is
/// not the same as declaring that it does not.
/// </param>
/// <param name="DeclaredUpdateMode">An update mode the entry names in so many words, when it names one.</param>
public sealed record MarketplaceEntry(
    string PluginName,
    string? RepositoryUrl,
    string? Revision,
    string? Version,
    bool? AutomaticUpdate,
    string? DeclaredUpdateMode);
