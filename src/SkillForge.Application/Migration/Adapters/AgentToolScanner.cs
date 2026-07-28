using SkillForge.Application.Abstractions;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration.Adapters;

/// <summary>
/// The three scans every adapter runs, and the presence bookkeeping around them.
/// </summary>
/// <remarks>
/// Passed to each adapter as one dependency instead of four, so an adapter's own code is a list of the paths its
/// provider uses and nothing else. That is the property worth protecting: when a provider moves a file, the diff
/// should be a path.
/// </remarks>
public sealed class AgentToolScanner
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the scanner.</summary>
    /// <param name="fileSystem">Used to test which of a provider's paths exist.</param>
    /// <param name="skills">Reads a provider's skill directory.</param>
    /// <param name="mcpServers">Reads a provider's MCP configuration.</param>
    /// <param name="instructionFiles">Finds a provider's instruction files.</param>
    public AgentToolScanner(
        IFileSystem fileSystem,
        SkillInventoryScanner skills,
        McpConfigurationScanner mcpServers,
        InstructionFileScanner instructionFiles)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(mcpServers);
        ArgumentNullException.ThrowIfNull(instructionFiles);

        _fileSystem = fileSystem;
        Skills = skills;
        McpServers = mcpServers;
        InstructionFiles = instructionFiles;
    }

    /// <summary>Gets the skill directory scanner.</summary>
    public SkillInventoryScanner Skills { get; }

    /// <summary>Gets the MCP configuration scanner.</summary>
    public McpConfigurationScanner McpServers { get; }

    /// <summary>Gets the instruction file scanner.</summary>
    public InstructionFileScanner InstructionFiles { get; }

    /// <summary>
    /// Keeps the paths that exist, which is what makes a provider "present".
    /// </summary>
    /// <param name="candidates">
    /// Every path the provider might use, files or directories. <see langword="null"/> entries are skipped, so an
    /// adapter can pass a project-scoped path unconditionally without inventing a stand-in when there is no
    /// project — an early version passed the provider's home directory instead and listed it three times.
    /// </param>
    /// <returns>The ones that exist, in the order given, each listed once.</returns>
    public IReadOnlyList<string> ExistingPaths(params string?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return
        [
            .. candidates
                .OfType<string>()
                .Where(path => _fileSystem.FileExists(path) || _fileSystem.DirectoryExists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Builds the presence record for a provider.
    /// </summary>
    /// <param name="providerId">The provider's identifier.</param>
    /// <param name="displayName">The provider's display name.</param>
    /// <param name="foundPaths">The paths that were found.</param>
    /// <returns>The presence record; absent when nothing was found.</returns>
    public static AgentToolPresence Presence(
        string providerId,
        string displayName,
        IReadOnlyList<string> foundPaths)
    {
        ArgumentNullException.ThrowIfNull(foundPaths);

        return new AgentToolPresence(providerId, displayName, foundPaths.Count > 0, foundPaths);
    }
}
