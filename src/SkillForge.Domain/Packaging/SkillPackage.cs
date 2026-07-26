namespace SkillForge.Domain.Packaging;

/// <summary>
/// A file included in a package.
/// </summary>
/// <param name="RelativePath">Path inside the archive, using <c>/</c> separators.</param>
/// <param name="SizeInBytes">Size of the file.</param>
/// <param name="Sha256">Hash of the file's contents, lowercase hexadecimal.</param>
public sealed record PackagedFile(string RelativePath, long SizeInBytes, string Sha256);

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
public sealed record SkillPackage(
    string SkillName,
    string Version,
    string ArchivePath,
    string HashPath,
    string ManifestPath,
    string ArchiveSha256,
    IReadOnlyList<PackagedFile> Files,
    DateTimeOffset CreatedAtUtc);
