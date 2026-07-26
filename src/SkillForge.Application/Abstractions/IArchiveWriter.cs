namespace SkillForge.Application.Abstractions;

/// <summary>
/// Builds an archive in memory from a set of files.
/// </summary>
/// <remarks>
/// Implementations must be deterministic: the same files with the same contents must produce byte-identical
/// output, which means a fixed entry order, a fixed timestamp and no ambient metadata. That is what lets a
/// consumer verify a package by rebuilding it.
/// </remarks>
public interface IArchiveWriter
{
    /// <summary>Builds an archive.</summary>
    /// <param name="entries">Files to include, in the order they should appear.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The archive's bytes.</returns>
    Task<byte[]> CreateAsync(IReadOnlyList<ArchiveEntry> entries, CancellationToken cancellationToken);
}

/// <summary>
/// One file to put in an archive.
/// </summary>
/// <param name="RelativePath">Path inside the archive, using <c>/</c> separators.</param>
/// <param name="Content">The file's bytes.</param>
public sealed record ArchiveEntry(string RelativePath, byte[] Content);

/// <summary>
/// Computes content hashes.
/// </summary>
public interface IHashCalculator
{
    /// <summary>Computes a SHA-256 hash.</summary>
    /// <param name="content">Bytes to hash.</param>
    /// <returns>The hash as lowercase hexadecimal.</returns>
    string ComputeSha256(ReadOnlySpan<byte> content);
}
