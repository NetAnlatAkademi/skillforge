namespace SkillForge.Domain.Provenance;

/// <summary>
/// How an asset gets its next version.
/// </summary>
/// <remarks>
/// The distinction a package hash alone cannot make. A plugin pinned to a commit and a plugin that updates itself
/// from a branch can carry the same hash today and different code tomorrow, and only one of them was reviewed.
///
/// <see cref="Unknown"/> is the default and stays the default. Nothing observable says "this updates manually", so
/// an asset whose distribution says nothing about updates is reported as unknown rather than as pinned.
/// </remarks>
public enum UpdateMode
{
    /// <summary>Nothing observed says how this asset updates.</summary>
    Unknown,

    /// <summary>The source names an immutable revision — a commit SHA, or a version the source treats as one.</summary>
    Pinned,

    /// <summary>The source names something that moves: a branch, a tag that is republished, or nothing at all.</summary>
    Floating,

    /// <summary>The distribution declares that it updates the asset without being asked.</summary>
    Automatic,
}
