using SkillForge.Application.Abstractions;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Modeling;

/// <summary>
/// Collects the skills a skill competes against.
/// </summary>
/// <remarks>
/// The distractors are the skill's **siblings** — the other skills installed beside it — because that is what it will
/// actually compete against in the collection it ships in. Inventing plausible-sounding decoys would measure how well
/// the description separates from decoys somebody made up, which is not a question anybody has.
///
/// A skill with no siblings gets no distractors, and the report says so rather than pretending the probe was as strong
/// as one with them.
/// </remarks>
public sealed class SkillCatalogue
{
    private readonly ISkillDiscovery _discovery;
    private readonly ISkillLoader _loader;

    /// <summary>Initialises the catalogue.</summary>
    /// <param name="discovery">Finds the sibling skill directories.</param>
    /// <param name="loader">Reads each sibling's name and description.</param>
    public SkillCatalogue(ISkillDiscovery discovery, ISkillLoader loader)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(loader);

        _discovery = discovery;
        _loader = loader;
    }

    /// <summary>
    /// Finds the skills installed beside the given one.
    /// </summary>
    /// <param name="skill">The skill under test.</param>
    /// <param name="cancellationToken">Token used to cancel the walk.</param>
    /// <returns>
    /// The siblings, by name and description, ordered by name so a probe is reproducible. Empty when the skill stands
    /// alone or its siblings cannot be read.
    /// </returns>
    public async Task<IReadOnlyList<SkillCandidate>> DistractorsAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var parent = Path.GetDirectoryName(skill.DirectoryPath.TrimEnd('/', '\\'));

        if (parent is null or { Length: 0 })
        {
            return [];
        }

        var candidates = new List<SkillCandidate>();

        foreach (var directory in _discovery.FindSkillDirectories(parent))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SamePath(directory, skill.DirectoryPath))
            {
                continue;
            }

            var load = await _loader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);

            // A sibling that will not load is skipped rather than reported: it is not the subject of this run, and its
            // own problems belong to its own validate.
            if (load.Value is { Name.Length: > 0, Description.Length: > 0 } sibling)
            {
                candidates.Add(new SkillCandidate(sibling.Name, sibling.Description));
            }
        }

        return [.. candidates.OrderBy(candidate => candidate.Name, StringComparer.Ordinal)];
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            left.Replace('\\', '/').TrimEnd('/'),
            right.Replace('\\', '/').TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
}
