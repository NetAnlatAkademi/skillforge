using SkillForge.Application.Abstractions;

namespace SkillForge.Cli.Tests;

/// <summary>
/// The slice of <see cref="IFileSystem"/> the command runners actually use, kept in memory.
/// </summary>
/// <remarks>
/// Deliberately its own small fake rather than a shared one: these tests only exercise writing output and
/// probing for an existing skill, and a fake that does less is a fake that explains itself.
/// </remarks>
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a file so <see cref="FileExists"/> reports it.</summary>
    internal FakeFileSystem WithFile(string path, string content = "")
    {
        _files[Normalise(path)] = content;
        return this;
    }

    /// <summary>Reads back what was written, so tests can assert on generated content.</summary>
    internal string ReadText(string path) => _files[Normalise(path)];

    public bool FileExists(string path) => _files.ContainsKey(Normalise(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalise(path));

    public string GetFullPath(string path) => Normalise(path);

    public string? ResolveLinkTarget(string path) => null;

    public long GetFileSizeInBytes(string path) => _files.TryGetValue(Normalise(path), out var c) ? c.Length : 0;

    public IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        var prefix = Normalise(directoryPath).TrimEnd('/') + "/";
        return _files.Keys.Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(_files[Normalise(path)]);

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(System.Text.Encoding.UTF8.GetBytes(_files[Normalise(path)]));

    public IEnumerable<string> EnumerateDirectories(string directoryPath) => [];

    public void CreateDirectory(string path) => _directories.Add(Normalise(path));

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        _files[Normalise(path)] = content;
        return Task.CompletedTask;
    }

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        _files[Normalise(path)] = System.Text.Encoding.UTF8.GetString(content);
        return Task.CompletedTask;
    }

    private static string Normalise(string path) =>
        '/' + string.Join('/', path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries));
}
