using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Diffing;

/// <summary>
/// Compares two versions of a skill by what they can do.
/// </summary>
/// <remarks>
/// Pure: two snapshots in, one diff out. Everything it needs was already computed by the loader, the inspector and
/// the validator, so this class only decides what counts as a change worth reporting.
/// </remarks>
public static class SkillSurfaceDiffer
{
    /// <summary>
    /// Diffs two versions of a skill.
    /// </summary>
    /// <param name="before">The earlier version.</param>
    /// <param name="after">The later version.</param>
    /// <returns>What changed about the surface.</returns>
    public static SkillSurfaceDiff Compare(SkillSnapshot before, SkillSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return new SkillSurfaceDiff(
            before.Path,
            after.Path,
            SurfaceValueChange.Between(NullIfEmpty(before.Skill.Name), NullIfEmpty(after.Skill.Name)),
            SurfaceValueChange.Between(before.Skill.Frontmatter.Version, after.Skill.Frontmatter.Version),
            SurfaceValueChange.Between(
                NullIfEmpty(before.Skill.Description),
                NullIfEmpty(after.Skill.Description)),
            SurfaceSetDiff.Between(before.Skill.Frontmatter.AllowedTools, after.Skill.Frontmatter.AllowedTools),
            SurfaceSetDiff.Between(before.Skill.Frontmatter.Compatibility, after.Skill.Frontmatter.Compatibility),
            SurfaceSetDiff.Between(Domains(before), Domains(after)),
            SurfaceSetDiff.Between(Scripts(before), Scripts(after)),
            SurfaceSetDiff.Between(Files(before), Files(after)),
            Findings(after).Except(Findings(before), FindingComparer).Select(finding => finding.Diagnostic).ToArray(),
            Findings(before).Except(Findings(after), FindingComparer).Select(finding => finding.Diagnostic).ToArray());
    }

    /// <summary>
    /// Compares findings by code and location rather than by message, so a reworded diagnostic is not reported as
    /// one finding resolved and another appearing.
    /// </summary>
    private static IEqualityComparer<Finding> FindingComparer { get; } =
        EqualityComparer<Finding>.Create(
            (left, right) => left.Key == right.Key,
            finding => finding.Key.GetHashCode(StringComparison.Ordinal));

    /// <summary>
    /// Hosts, not full URLs: a link changing from <c>/docs/a</c> to <c>/docs/b</c> on the same host is not a
    /// change in who the skill talks to, and that is the question a reviewer is asking.
    /// </summary>
    private static IEnumerable<string> Domains(SkillSnapshot snapshot) =>
        snapshot.Inspection.ExternalUrls
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Host : url)
            .Where(host => host.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Scripts(SkillSnapshot snapshot) =>
        snapshot.Skill.Resources
            .Where(resource => resource.Kind == SkillResourceKind.Script)
            .Select(resource => resource.RelativePath);

    private static IEnumerable<string> Files(SkillSnapshot snapshot) =>
        snapshot.Skill.Resources.Select(resource => resource.RelativePath);

    private static IEnumerable<Finding> Findings(SkillSnapshot snapshot) =>
        snapshot.Report.Diagnostics.Select(diagnostic => new Finding(diagnostic));

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>A diagnostic keyed by what identifies it across versions.</summary>
    private readonly record struct Finding(Diagnostic Diagnostic)
    {
        internal string Key => $"{Diagnostic.Code}|{Diagnostic.FilePath}|{Diagnostic.Line}";
    }
}
