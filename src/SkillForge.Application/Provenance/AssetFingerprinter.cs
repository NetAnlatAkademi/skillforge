using System.Text;
using SkillForge.Application.Abstractions;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Computes a content fingerprint for an asset's directory.
/// </summary>
/// <remarks>
/// The fingerprint is what makes "this is not the copy that was reviewed" a computable statement, so its only real
/// requirement is that the same bytes produce the same value on any machine. It hashes each file, sorts the
/// per-file lines by path with an ordinal comparison, and hashes the result — paths are normalised to forward
/// slashes so a Windows checkout and a Linux one agree.
///
/// A file that cannot be read is included as a line saying so rather than skipped. Skipping it would let an
/// unreadable file change the contents of an asset without changing its fingerprint, which is the one thing a
/// fingerprint must never allow.
/// </remarks>
public sealed class AssetFingerprinter
{
    /// <summary>Directories that never contribute to a fingerprint.</summary>
    /// <remarks>
    /// The same list <c>pack</c> excludes from an archive and <c>SkillDiscovery</c> refuses to walk into. Build
    /// output and a <c>.git</c> directory are not the asset, and letting them in would make the fingerprint change
    /// every time somebody ran a build.
    /// </remarks>
    private static readonly string[] ExcludedDirectories =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    /// <summary>Files that never contribute to a fingerprint.</summary>
    private static readonly string[] ExcludedFiles = [".DS_Store", "Thumbs.db"];

    /// <summary>
    /// The largest file whose contents are read into memory at once.
    /// </summary>
    /// <remarks>
    /// A fingerprint is computed over trees that came from somewhere else — a marketplace, a fork, a plugin
    /// somebody installed — so the input is untrusted by definition, and reading it whole is a decision about how
    /// much memory a stranger gets to allocate. Ten megabytes is far past anything a skill legitimately ships and
    /// far below anything that hurts.
    ///
    /// A file over the cap still contributes: its path and its **size** are hashed in place of its contents, so it
    /// cannot be swapped for a different file of a different length without the fingerprint moving. Two different
    /// 40 MB files of identical length would collide, which is the honest cost of the bound and is stated here
    /// rather than hidden.
    /// </remarks>
    private const long MaximumFileBytes = 10 * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly IHashCalculator _hashCalculator;

    /// <summary>Initialises the fingerprinter.</summary>
    /// <param name="fileSystem">Reads the asset's files.</param>
    /// <param name="hashCalculator">Hashes them.</param>
    public AssetFingerprinter(IFileSystem fileSystem, IHashCalculator hashCalculator)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(hashCalculator);

        _fileSystem = fileSystem;
        _hashCalculator = hashCalculator;
    }

    /// <summary>Fingerprints everything under a directory.</summary>
    /// <param name="directory">Directory to fingerprint.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// The fingerprint as lowercase hexadecimal. A directory with no files still produces one — the hash of an
    /// empty list — because "there is nothing here" is a content fact like any other.
    /// </returns>
    public async Task<string> ComputeAsync(string directory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = _fileSystem.GetFullPath(directory);
        var lines = new List<string>();

        foreach (var file in _fileSystem.EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (IsExcluded(relative))
            {
                continue;
            }

            string hash;
            try
            {
                var size = _fileSystem.GetFileSizeInBytes(file);

                hash = size > MaximumFileBytes
                    ? $"oversize-{size}"
                    : _hashCalculator.ComputeSha256(
                        await _fileSystem.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                hash = "unreadable";
            }

            lines.Add($"{hash}  {relative}");
        }

        lines.Sort(StringComparer.Ordinal);

        return _hashCalculator.ComputeSha256(
            Encoding.UTF8.GetBytes(string.Join('\n', lines) + '\n'));
    }

    private static bool IsExcluded(string relativePath)
    {
        var segments = relativePath.Split('/');

        return segments[..^1].Any(segment =>
                ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase))
            || ExcludedFiles.Contains(segments[^1], StringComparer.OrdinalIgnoreCase);
    }
}
