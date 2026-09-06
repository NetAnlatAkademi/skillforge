namespace SkillForge.Domain.Provenance;

/// <summary>
/// One asset, and everything observed about where it came from.
/// </summary>
/// <param name="Name">The asset's declared name, or its directory name when it declares none.</param>
/// <param name="Kind">Whether it is a skill or a plugin.</param>
/// <param name="Path">
/// Path of the asset's directory, relative to the repository root when it is inside one and absolute otherwise. A
/// build agent's absolute path means nothing to whoever reads the report later.
/// </param>
/// <param name="Source">Where it is distributed from.</param>
/// <param name="Status">What could be established about its relationship to that source.</param>
/// <param name="DeclaredUpstream">
/// An upstream the asset's own manifest names, or <see langword="null"/>. Never inferred: guessing which public
/// repository a skill was copied out of would be presented as fact and read as one.
/// </param>
/// <param name="LocallyModifiedFiles">
/// How many of the asset's files have uncommitted changes. Zero outside a repository, where nothing can be said to
/// be modified against anything.
/// </param>
/// <param name="Evidence">
/// The files each observation came from, ordered. A provenance report that cannot say where it read something is
/// asking to be trusted rather than checked.
/// </param>
/// <param name="DistributedBy">
/// The plugin this asset's marketplace, publisher and update mode were inherited from, or <see langword="null"/>
/// when they are its own.
/// </param>
/// <remarks>
/// <paramref name="DistributedBy"/> exists so a diff can report a publisher change **once**. A plugin holding
/// twenty skills would otherwise produce twenty-one identical findings for the same edit, and a finding repeated
/// per file is one people learn to scroll past.
/// </remarks>
public sealed record AssetProvenance(
    string Name,
    AssetKind Kind,
    string Path,
    DistributionSource Source,
    ProvenanceStatus Status,
    string? DeclaredUpstream,
    int LocallyModifiedFiles,
    IReadOnlyList<string> Evidence,
    string? DistributedBy = null);
