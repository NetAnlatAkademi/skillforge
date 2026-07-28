using SkillForge.Application.Abstractions;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Migration;

/// <summary>
/// Lists the skills installed in a provider's skill directory.
/// </summary>
/// <remarks>
/// Shared by the adapters, because "a directory whose subdirectories each hold a <c>SKILL.md</c>" is the one
/// convention every provider that supports skills has in common. Where that directory <em>is</em> stays the
/// adapter's business.
///
/// It loads each skill through <see cref="ISkillLoader"/> rather than reading the frontmatter again. A skill that
/// fails to load is still listed, under its directory name, because it is installed either way and an inventory
/// that quietly omitted it would be wrong about what is on the machine — `validate` is where a broken skill gets
/// judged.
/// </remarks>
public sealed class SkillInventoryScanner
{
    private readonly IFileSystem _fileSystem;
    private readonly ISkillLoader _loader;

    /// <summary>Initialises the scanner.</summary>
    /// <param name="fileSystem">Used to find the skill directories.</param>
    /// <param name="loader">Used to read each skill's own declarations.</param>
    public SkillInventoryScanner(IFileSystem fileSystem, ISkillLoader loader)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(loader);

        _fileSystem = fileSystem;
        _loader = loader;
    }

    /// <summary>
    /// Reads every skill under a directory.
    /// </summary>
    /// <param name="providerId">Provider to attribute the skills to.</param>
    /// <param name="skillsDirectory">Directory whose subdirectories are skills.</param>
    /// <param name="cancellationToken">Token used to cancel the scan.</param>
    /// <returns>The skills found, ordered by name. Empty when the directory does not exist.</returns>
    public async Task<IReadOnlyList<SkillInventoryEntry>> ScanAsync(
        string providerId,
        string skillsDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(skillsDirectory);

        if (!_fileSystem.DirectoryExists(skillsDirectory))
        {
            return [];
        }

        var entries = new List<SkillInventoryEntry>();

        foreach (var directory in _fileSystem.EnumerateDirectories(skillsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_fileSystem.FileExists(Path.Combine(directory, SkillDefinition.SkillFileName)))
            {
                continue;
            }

            entries.Add(await ReadEntryAsync(providerId, directory, cancellationToken).ConfigureAwait(false));
        }

        return [.. entries.OrderBy(entry => entry.Name, StringComparer.Ordinal)];
    }

    private async Task<SkillInventoryEntry> ReadEntryAsync(
        string providerId,
        string directory,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);
        var fallbackName = Path.GetFileName(directory.TrimEnd('/', '\\'));

        return load.Value is { } skill
            ? new SkillInventoryEntry(
                providerId,
                skill.Name.Length > 0 ? skill.Name : fallbackName,
                directory,
                skill.Frontmatter.Compatibility)
            : new SkillInventoryEntry(providerId, fallbackName, directory, []);
    }
}
