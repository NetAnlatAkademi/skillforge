namespace SkillForge.Domain.Diffing;

/// <summary>
/// What appeared and what disappeared in one part of a skill's surface.
/// </summary>
/// <param name="Added">Entries present after but not before, ordered.</param>
/// <param name="Removed">Entries present before but not after, ordered.</param>
public sealed record SurfaceSetDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    /// <summary>Nothing changed.</summary>
    public static SurfaceSetDiff Unchanged { get; } = new([], []);

    /// <summary>Gets a value indicating whether anything changed.</summary>
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0;

    /// <summary>
    /// Compares two sets of names.
    /// </summary>
    /// <param name="before">Names before the change.</param>
    /// <param name="after">Names after the change.</param>
    /// <returns>The difference, with both sides ordered so a report is reproducible.</returns>
    public static SurfaceSetDiff Between(
        IEnumerable<string> before,
        IEnumerable<string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeSet = before.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var afterSet = after.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SurfaceSetDiff(
            [.. afterSet.Except(beforeSet, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)],
            [.. beforeSet.Except(afterSet, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)]);
    }
}
