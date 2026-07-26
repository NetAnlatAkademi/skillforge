using SkillForge.Application.Abstractions;

namespace SkillForge.Application.Skills;

/// <summary>
/// Decides whether a path is allowed to be read as part of a skill.
/// </summary>
/// <remarks>
/// A skill must not reach outside its own directory. Two things can break that: a path containing
/// <c>..</c> segments, and a symbolic link whose target lives elsewhere. Both are checked against the
/// normalised absolute form of the skill root.
/// </remarks>
public sealed class SkillPathGuard
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the guard.</summary>
    /// <param name="fileSystem">File system used to normalise paths and resolve links.</param>
    public SkillPathGuard(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Path comparison for the current platform. Windows and macOS compare paths case-insensitively.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Determines whether a path stays inside the skill directory, following links.
    /// </summary>
    /// <param name="skillDirectory">Normalised absolute path of the skill directory.</param>
    /// <param name="candidatePath">Path to check. May be relative to <paramref name="skillDirectory"/>.</param>
    /// <returns><see langword="true"/> when the effective target is inside the skill directory.</returns>
    public bool IsInsideSkillDirectory(string skillDirectory, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        var root = _fileSystem.GetFullPath(skillDirectory);
        var resolved = _fileSystem.GetFullPath(Path.IsPathRooted(candidatePath)
            ? candidatePath
            : Path.Combine(skillDirectory, candidatePath));

        if (!IsUnder(root, resolved))
        {
            return false;
        }

        // A link inside the directory may still point outside it.
        var linkTarget = _fileSystem.ResolveLinkTarget(resolved);
        return linkTarget is null || IsUnder(root, _fileSystem.GetFullPath(linkTarget));
    }

    /// <summary>
    /// Converts an absolute path into a skill-relative path using <c>/</c> as separator.
    /// </summary>
    /// <param name="skillDirectory">Normalised absolute path of the skill directory.</param>
    /// <param name="absolutePath">Absolute path inside the skill directory.</param>
    /// <returns>The relative path, with forward slashes.</returns>
    public string ToRelativePath(string skillDirectory, string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var relative = Path.GetRelativePath(
            _fileSystem.GetFullPath(skillDirectory),
            _fileSystem.GetFullPath(absolutePath));

        return relative.Replace('\\', '/');
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is the root itself or lives beneath it.
    /// </summary>
    /// <remarks>
    /// Both paths are reduced to a single separator form first. Comparing raw strings would fail on
    /// Windows, where a path may legitimately arrive with either <c>\</c> or <c>/</c>.
    /// </remarks>
    private static bool IsUnder(string root, string candidate)
    {
        var comparableRoot = ToComparableForm(root);
        var comparableCandidate = ToComparableForm(candidate);

        return string.Equals(comparableCandidate, comparableRoot, PathComparison)
            || comparableCandidate.StartsWith(comparableRoot + '/', PathComparison);
    }

    private static string ToComparableForm(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
