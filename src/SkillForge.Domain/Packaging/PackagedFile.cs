namespace SkillForge.Domain.Packaging;

/// <summary>
/// A file included in a package.
/// </summary>
/// <param name="RelativePath">Path inside the archive, using <c>/</c> separators.</param>
/// <param name="SizeInBytes">Size of the file.</param>
/// <param name="Sha256">Hash of the file's contents, lowercase hexadecimal.</param>
public sealed record PackagedFile(string RelativePath, long SizeInBytes, string Sha256);
