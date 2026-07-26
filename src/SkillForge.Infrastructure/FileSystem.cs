using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure;

/// <summary>
/// <see cref="IFileSystem"/> backed by the real file system.
/// </summary>
/// <remarks>
/// Deliberately thin: it translates calls to <c>System.IO</c> and normalises paths, and contains no
/// skill-specific logic. Enumeration ignores directories the process cannot read rather than failing the
/// whole operation, because a skill directory may sit under a tree with restricted siblings.
/// </remarks>
public sealed class FileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public string GetFullPath(string path) => Path.GetFullPath(path);

    /// <inheritdoc />
    public string? ResolveLinkTarget(string path)
    {
        // Resolve the whole chain, not just one hop: a link to a link can still land outside the skill.
        var target = File.Exists(path)
            ? File.ResolveLinkTarget(path, returnFinalTarget: true)
            : Directory.ResolveLinkTarget(path, returnFinalTarget: true);

        return target is null ? null : Path.GetFullPath(target.FullName);
    }

    /// <inheritdoc />
    public long GetFileSizeInBytes(string path) => new FileInfo(path).Length;

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
            MatchType = MatchType.Simple,
        };

        return Directory.EnumerateFiles(directoryPath, "*", options);
    }

    /// <inheritdoc />
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);
}
