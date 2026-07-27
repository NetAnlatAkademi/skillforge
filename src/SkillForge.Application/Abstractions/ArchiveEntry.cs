namespace SkillForge.Application.Abstractions;

/// <summary>
/// One file to put in an archive.
/// </summary>
/// <param name="RelativePath">Path inside the archive, using <c>/</c> separators.</param>
/// <param name="Content">The file's bytes.</param>
public sealed record ArchiveEntry(string RelativePath, byte[] Content);
