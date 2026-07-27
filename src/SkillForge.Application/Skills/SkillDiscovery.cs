using SkillForge.Application.Abstractions;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Skills;

/// <summary>
/// Finds skills by looking for <c>SKILL.md</c> files.
/// </summary>
/// <remarks>
/// A directory is a skill because it contains a <c>SKILL.md</c> — there is no manifest listing skills, so the
/// entry point is the only reliable marker.
///
/// Two rules keep the result sane. Tooling directories are skipped, so a <c>node_modules</c> tree that happens
/// to vendor a skill does not turn up in a report. And a skill found inside another skill is ignored: nesting
/// is not a thing the format supports, so such a file is far more likely to be an example or a fixture shipped
/// by the outer skill than a second skill to validate.
/// </remarks>
public sealed class SkillDiscovery : ISkillDiscovery
{
    private static readonly string[] IgnoredDirectoryNames =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the discovery.</summary>
    /// <param name="fileSystem">File system used to walk the tree.</param>
    public SkillDiscovery(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindSkillDirectories(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!_fileSystem.DirectoryExists(rootDirectory))
        {
            return [];
        }

        var root = _fileSystem.GetFullPath(rootDirectory);

        var candidates = _fileSystem.EnumerateFiles(root)
            .Where(IsSkillFile)
            .Select(file => Path.GetDirectoryName(file))
            .Where(directory => directory is { Length: > 0 })
            .Select(directory => _fileSystem.GetFullPath(directory!))
            .Where(directory => !IsUnderIgnoredDirectory(root, directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Ordered shortest-first, so the outer skill is always seen before anything nested inside it.
        var accepted = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (!accepted.Any(outer => IsInside(outer, candidate)))
            {
                accepted.Add(candidate);
            }
        }

        return accepted;
    }

    private static bool IsSkillFile(string path) =>
        string.Equals(
            Path.GetFileName(path),
            SkillDefinition.SkillFileName,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderIgnoredDirectory(string root, string directory)
    {
        var relative = Path.GetRelativePath(root, directory).Replace('\\', '/');

        return relative.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsInside(string outer, string candidate)
    {
        var prefix = outer.Replace('\\', '/').TrimEnd('/') + '/';

        return candidate.Replace('\\', '/')
            .StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
