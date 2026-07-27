namespace SkillForge.Domain.Inspection;

/// <summary>
/// Capability names used in inspection output. Deliberately plain English rather than a permission grammar:
/// the first release describes what it sees and leaves policy to the reader.
/// </summary>
public static class SkillCapabilities
{
    /// <summary>The skill ships files an agent is expected to read.</summary>
    public const string FilesystemRead = "Filesystem Read";

    /// <summary>The skill ships an executable script.</summary>
    public const string ShellExecution = "Shell Execution";

    /// <summary>The skill points at something on the network.</summary>
    public const string NetworkAccess = "Network Access";

    /// <summary>The skill ships a binary file whose contents cannot be reviewed as text.</summary>
    public const string BinaryContent = "Binary Content";
}
