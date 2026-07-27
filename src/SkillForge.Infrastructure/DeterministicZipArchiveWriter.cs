using System.IO.Compression;
using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure;

/// <summary>
/// Writes ZIP archives whose bytes depend only on their contents.
/// </summary>
/// <remarks>
/// Three things would otherwise make two builds of the same skill differ: entry order, entry timestamps and
/// the platform's directory separator. Entries are written in the order given (the packager sorts them), every
/// timestamp is pinned to a fixed instant, and paths always use <c>/</c>. The fixed date is 1980-01-01, the
/// earliest a ZIP entry can express — a real timestamp would make the hash change on every build, which
/// would defeat the point of publishing one.
/// </remarks>
public sealed class DeterministicZipArchiveWriter : IArchiveWriter
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public async Task<byte[]> CreateAsync(
        IReadOnlyList<ArchiveEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var buffer = new MemoryStream();

        // Scoped so the archive is fully flushed before the buffer is read.
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var archiveEntry = archive.CreateEntry(
                    entry.RelativePath.Replace('\\', '/'),
                    CompressionLevel.Optimal);

                archiveEntry.LastWriteTime = FixedTimestamp;

                await using var stream = archiveEntry.Open();
                await stream.WriteAsync(entry.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return buffer.ToArray();
    }
}
