namespace SkillForge.Domain.Migration;

/// <summary>
/// An instruction file a provider reads — <c>CLAUDE.md</c>, <c>AGENTS.md</c>, <c>copilot-instructions.md</c>.
/// </summary>
/// <remarks>
/// The contents are not carried. Whether two instruction files contradict each other is a judgement about prose
/// that SkillForge cannot make honestly, so the report names the files that are in play and leaves the reading to
/// the person migrating. The size is there because it is the one fact that tells them how much reading that is.
/// </remarks>
/// <param name="ProviderId">Identifier of the provider that reads this file.</param>
/// <param name="Path">Absolute path of the file.</param>
/// <param name="Scope">Whether it applies to the whole machine or to one project.</param>
/// <param name="SizeInBytes">The file's size.</param>
public sealed record InstructionFileReference(
    string ProviderId,
    string Path,
    InstructionScope Scope,
    long SizeInBytes);
