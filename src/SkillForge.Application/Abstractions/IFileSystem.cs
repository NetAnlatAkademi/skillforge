namespace SkillForge.Application.Abstractions;

/// <summary>
/// The only way the Application layer touches the file system.
/// </summary>
/// <remarks>
/// Every use case depends on this abstraction rather than <c>System.IO</c> directly, so rules can be
/// unit tested against an in-memory layout without creating temporary directories.
/// </remarks>
public interface IFileSystem
{
    /// <summary>Determines whether a file exists at the given path.</summary>
    /// <param name="path">Path to probe.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    bool FileExists(string path);

    /// <summary>Determines whether a directory exists at the given path.</summary>
    /// <param name="path">Path to probe.</param>
    /// <returns><see langword="true"/> when the directory exists.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Converts a path into a normalised absolute path, resolving <c>.</c> and <c>..</c> segments.
    /// </summary>
    /// <param name="path">Absolute or relative path.</param>
    /// <returns>The normalised absolute path.</returns>
    string GetFullPath(string path);

    /// <summary>
    /// Resolves the final target of a symbolic link or junction.
    /// </summary>
    /// <param name="path">Path that may be a link.</param>
    /// <returns>
    /// The normalised absolute path the link ultimately points at, or <see langword="null"/> when
    /// <paramref name="path"/> is not a link.
    /// </returns>
    string? ResolveLinkTarget(string path);

    /// <summary>Gets the size of a file in bytes.</summary>
    /// <param name="path">Path of the file.</param>
    /// <returns>Size in bytes.</returns>
    long GetFileSizeInBytes(string path);

    /// <summary>
    /// Enumerates every file under a directory, recursively.
    /// </summary>
    /// <param name="directoryPath">Directory to walk.</param>
    /// <returns>Absolute paths of the files found, in unspecified order.</returns>
    IEnumerable<string> EnumerateFiles(string directoryPath);

    /// <summary>
    /// Enumerates the immediate subdirectories of a directory.
    /// </summary>
    /// <param name="directoryPath">Directory to look in.</param>
    /// <returns>
    /// Absolute paths of the subdirectories found, in unspecified order. Empty when the directory does not exist,
    /// because "the provider is not installed" is an ordinary answer here rather than an error.
    /// </returns>
    IEnumerable<string> EnumerateDirectories(string directoryPath);

    /// <summary>Reads a file's raw bytes.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The file's bytes.</returns>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Creates a directory, including any missing parents. Does nothing if it exists.</summary>
    /// <param name="path">Directory to create.</param>
    void CreateDirectory(string path);

    /// <summary>Writes a text file, replacing it if it exists.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="content">Text to write.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Writes a binary file, replacing it if it exists.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="content">Bytes to write.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Reads a text file in full.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The file's contents.</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
}
