using SkillForge.Application.Abstractions;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IFileSystem"/> so loader behaviour can be tested without touching disk.
/// </summary>
/// <remarks>
/// Paths are stored exactly as given and compared case-insensitively, matching Windows and macOS. Tests
/// use forward slashes and a rooted prefix; <see cref="GetFullPath"/> only collapses <c>.</c> and
/// <c>..</c> segments rather than consulting the real working directory.
/// </remarks>
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase);

    internal FakeFileSystem AddFile(string path, string content)
    {
        var normalised = Normalise(path);
        _files[normalised] = content;

        var directory = GetParent(normalised);
        while (directory is not null)
        {
            _directories.Add(directory);
            directory = GetParent(directory);
        }

        return this;
    }

    internal FakeFileSystem AddDirectory(string path)
    {
        _directories.Add(Normalise(path));
        return this;
    }

    /// <summary>Registers <paramref name="path"/> as a link pointing at <paramref name="target"/>.</summary>
    internal FakeFileSystem AddLink(string path, string target)
    {
        var normalised = Normalise(path);
        _files[normalised] = string.Empty;
        _links[normalised] = Normalise(target);
        _directories.Add(GetParent(normalised)!);
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalise(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalise(path));

    public string GetFullPath(string path) => Normalise(path);

    public string? ResolveLinkTarget(string path) =>
        _links.TryGetValue(Normalise(path), out var target) ? target : null;

    public long GetFileSizeInBytes(string path) =>
        _files.TryGetValue(Normalise(path), out var content) ? content.Length : 0;

    public IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        var prefix = Normalise(directoryPath).TrimEnd('/') + "/";
        return _files.Keys
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        _files.TryGetValue(Normalise(path), out var content)
            ? Task.FromResult(content)
            : throw new FileNotFoundException($"Fake file system has no file at '{path}'.", path);

    /// <summary>
    /// Collapses separators and relative segments. Mirrors what <c>Path.GetFullPath</c> does to the
    /// inputs these tests use, without depending on the real current directory.
    /// </summary>
    private static string Normalise(string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resolved = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case ".":
                    continue;
                case ".." when resolved.Count > 0:
                    resolved.RemoveAt(resolved.Count - 1);
                    continue;
                default:
                    resolved.Add(segment);
                    continue;
            }
        }

        return '/' + string.Join('/', resolved);
    }

    private static string? GetParent(string normalisedPath)
    {
        var index = normalisedPath.LastIndexOf('/');
        return index <= 0 ? null : normalisedPath[..index];
    }
}
