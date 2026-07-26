namespace SkillForge.Domain.Diagnostics;

/// <summary>
/// The complete set of diagnostic codes SkillForge can emit.
/// </summary>
/// <remarks>
/// Codes are part of the public contract: CI configurations and suppression files refer to them, so a
/// released code is never renumbered, reused for a different rule, or removed. Every code listed here
/// is documented in <c>docs/validation-rules.md</c>.
/// </remarks>
public static class DiagnosticCodes
{
    /// <summary>
    /// <c>SKILL.md</c> was not found, or exists but could not be read. Both read the same way to the
    /// person running the CLI: SkillForge could not get at the skill.
    /// </summary>
    public const string SkillFileNotFound = "SF0001";

    /// <summary>The YAML frontmatter block was not found.</summary>
    public const string FrontmatterNotFound = "SF0002";

    /// <summary>The YAML frontmatter could not be parsed.</summary>
    public const string FrontmatterNotParsable = "SF0003";

    /// <summary>The <c>name</c> field is missing.</summary>
    public const string NameMissing = "SF0004";

    /// <summary>The <c>description</c> field is missing.</summary>
    public const string DescriptionMissing = "SF0005";

    /// <summary>The skill name is not a valid identifier.</summary>
    public const string NameInvalid = "SF0006";

    /// <summary>A file referenced by the skill does not exist.</summary>
    public const string ReferencedFileNotFound = "SF0007";

    /// <summary>A path escapes the skill directory.</summary>
    public const string PathEscapesSkillDirectory = "SF0008";

    /// <summary>The same metadata field is declared more than once.</summary>
    public const string DuplicateMetadataField = "SF0009";

    /// <summary>The package version is not a valid version string.</summary>
    public const string PackageVersionInvalid = "SF0010";

    /// <summary>The description is too short to be useful.</summary>
    public const string DescriptionTooShort = "SF1001";

    /// <summary>The description does not state when the skill should activate.</summary>
    public const string DescriptionWithoutActivationContext = "SF1002";

    /// <summary><c>SKILL.md</c> is longer than the recommended length.</summary>
    public const string SkillFileTooLong = "SF1003";

    /// <summary>A file in the skill directory is never referenced.</summary>
    public const string UnusedFile = "SF1004";

    /// <summary>The skill references an external URL.</summary>
    public const string ExternalUrlPresent = "SF1005";

    /// <summary>The skill ships a script but declares no permission for it.</summary>
    public const string ScriptWithoutDeclaredPermission = "SF1006";

    /// <summary>A shell command requests broad privileges.</summary>
    public const string BroadShellPrivileges = "SF1007";

    /// <summary>Package dependencies are not pinned to specific versions.</summary>
    public const string UnpinnedDependencies = "SF1008";

    /// <summary>No license is declared.</summary>
    public const string LicenseMissing = "SF1009";

    /// <summary>No agent compatibility information is declared.</summary>
    public const string CompatibilityMissing = "SF1010";

    /// <summary>The skill contains a script.</summary>
    public const string ContainsScript = "SF2001";

    /// <summary>The skill contains an external URL.</summary>
    public const string ContainsExternalUrl = "SF2002";

    /// <summary>The skill contains a binary file.</summary>
    public const string ContainsBinaryFile = "SF2003";

    /// <summary>The skill contains an <c>evals</c> folder.</summary>
    public const string ContainsEvals = "SF2004";
}
