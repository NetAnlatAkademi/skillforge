namespace SkillForge.Domain.Skills;

/// <summary>
/// A file that belongs to a skill.
/// </summary>
/// <param name="RelativePath">
/// Path relative to the skill directory, always using <c>/</c> as separator so that reports are
/// identical on Windows and Linux.
/// </param>
/// <param name="AbsolutePath">Normalised absolute path on the current machine.</param>
/// <param name="Kind">What the file looks like, inferred from its extension.</param>
/// <param name="SizeInBytes">Size of the file on disk.</param>
public sealed record SkillResource(
    string RelativePath,
    string AbsolutePath,
    SkillResourceKind Kind,
    long SizeInBytes);
