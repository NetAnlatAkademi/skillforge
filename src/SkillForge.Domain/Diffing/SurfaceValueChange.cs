namespace SkillForge.Domain.Diffing;

/// <summary>
/// A single value that changed between two versions of a skill.
/// </summary>
/// <param name="Before">The earlier value, or <see langword="null"/> when there was none.</param>
/// <param name="After">The later value, or <see langword="null"/> when there is none.</param>
public sealed record SurfaceValueChange(string? Before, string? After)
{
    /// <summary>
    /// Returns a change, or <see langword="null"/> when the two values are the same.
    /// </summary>
    /// <param name="before">The earlier value.</param>
    /// <param name="after">The later value.</param>
    /// <returns>The change, or <see langword="null"/> when nothing changed.</returns>
    public static SurfaceValueChange? Between(string? before, string? after) =>
        string.Equals(before, after, StringComparison.Ordinal) ? null : new SurfaceValueChange(before, after);
}
