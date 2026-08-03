using SkillForge.Domain.Provenance;

namespace SkillForge.Domain.Packaging;

/// <summary>
/// The result of packaging a skill.
/// </summary>
/// <param name="SkillName">Name of the packaged skill.</param>
/// <param name="Version">Version the package was built as.</param>
/// <param name="ArchivePath">Absolute path of the archive.</param>
/// <param name="HashPath">Absolute path of the file containing the archive's hash.</param>
/// <param name="ManifestPath">Absolute path of the manifest.</param>
/// <param name="ArchiveSha256">Hash of the archive, lowercase hexadecimal.</param>
/// <param name="Files">Files included, ordered by path.</param>
/// <param name="CreatedAtUtc">When the package was built, in UTC.</param>
/// <param name="Provenance">Where the packaged skill came from, as far as it could be observed.</param>
public sealed record SkillPackage(
    string SkillName,
    string Version,
    string ArchivePath,
    string HashPath,
    string ManifestPath,
    string ArchiveSha256,
    IReadOnlyList<PackagedFile> Files,
    DateTimeOffset CreatedAtUtc,
    SkillProvenance Provenance);
