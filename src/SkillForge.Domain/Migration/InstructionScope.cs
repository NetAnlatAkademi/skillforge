namespace SkillForge.Domain.Migration;

/// <summary>
/// How far an instruction file or configuration reaches.
/// </summary>
public enum InstructionScope
{
    /// <summary>Applies wherever the provider runs, from the user's home directory.</summary>
    User = 0,

    /// <summary>Applies to one project, from the inspected directory.</summary>
    Project,
}
