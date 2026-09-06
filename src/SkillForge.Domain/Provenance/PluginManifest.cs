namespace SkillForge.Domain.Provenance;

/// <summary>
/// What a plugin's own manifest says about itself.
/// </summary>
/// <remarks>
/// A claim, not a verification: the manifest is written by whoever ships the plugin, so a publisher named here is
/// the publisher the plugin says it has. That is still worth recording — the useful question is whether it changed
/// between two revisions, and a claim that changes is evidence even when the claim itself is unchecked.
/// </remarks>
/// <param name="Name">The plugin's declared name.</param>
/// <param name="Version">Declared version, or <see langword="null"/>.</param>
/// <param name="Publisher">Author or owner as the manifest names them.</param>
/// <param name="RepositoryUrl">Repository the manifest points at.</param>
/// <param name="Path">The manifest file this was read from.</param>
public sealed record PluginManifest(
    string Name,
    string? Version,
    string? Publisher,
    string? RepositoryUrl,
    string Path);
