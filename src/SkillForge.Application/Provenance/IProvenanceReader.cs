using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Records where a skill came from.
/// </summary>
public interface IProvenanceReader
{
    /// <summary>Reads what can be observed about a skill's origin.</summary>
    /// <param name="skillDirectory">Directory the skill lives in.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// The provenance. Never <see langword="null"/> and never fails: a skill outside a repository still has a tool
    /// version and a timestamp, and the fields that could not be observed stay unset.
    /// </returns>
    ValueTask<SkillProvenance> ReadAsync(string skillDirectory, CancellationToken cancellationToken = default);
}
