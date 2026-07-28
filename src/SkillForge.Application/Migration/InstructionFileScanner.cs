using SkillForge.Application.Abstractions;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// Lists which of a provider's instruction files are actually present.
/// </summary>
/// <remarks>
/// It records the path, the scope and the size, and never the contents. Whether two instruction files contradict
/// each other is a judgement about prose, and SkillForge saying "these conflict" would be inventing a reading of
/// somebody's English. Naming the files that are in play, with how much text each one is, is the honest half of
/// that answer.
/// </remarks>
public sealed class InstructionFileScanner
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the scanner.</summary>
    /// <param name="fileSystem">Used to test for the files and read their sizes.</param>
    public InstructionFileScanner(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Keeps the candidates that exist.
    /// </summary>
    /// <param name="providerId">Provider that reads these files.</param>
    /// <param name="candidates">Candidate paths with the scope each one applies at.</param>
    /// <returns>References to the files that are present, in the order given.</returns>
    public IReadOnlyList<InstructionFileReference> Scan(
        string providerId,
        IEnumerable<(string Path, InstructionScope Scope)> candidates)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(candidates);

        return
        [
            .. candidates
                .Where(candidate => _fileSystem.FileExists(candidate.Path))
                .Select(candidate => new InstructionFileReference(
                    providerId,
                    candidate.Path,
                    candidate.Scope,
                    _fileSystem.GetFileSizeInBytes(candidate.Path))),
        ];
    }
}
