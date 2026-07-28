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

    /// <summary>
    /// A reference leaves the skill directory but points at a sibling — normal inside a collection of skills,
    /// but it cannot be satisfied by the skill on its own.
    /// </summary>
    public const string ReferenceLeavesSkill = "SF1011";

    /// <summary>
    /// <c>skillforge.yaml</c> exists but could not be parsed, so its settings were ignored.
    /// </summary>
    public const string ConfigurationNotParsable = "SF1012";

    /// <summary>
    /// A <c>version</c> field was found at the top level of the frontmatter, where the schema does not look for
    /// it. SkillForge reads it anyway rather than losing it silently, and says so.
    /// </summary>
    public const string VersionOutsideMetadata = "SF1013";

    /// <summary>
    /// A file under <c>evals</c> could not be read or parsed, so its cases were skipped. Reported rather than
    /// fatal, for the same reason as SF1012: the rest of the suite is still worth running.
    /// </summary>
    public const string EvalFileNotParsable = "SF1014";

    /// <summary>
    /// A provider's own configuration file was found but could not be read or parsed, so what it declares is
    /// missing from the migration inventory. Same shape as SF1012 and SF1014: reported rather than fatal, because
    /// the rest of the inventory is still worth having and a silent gap would look like an empty configuration.
    /// </summary>
    public const string ProviderConfigurationNotParsable = "SF1015";

    /// <summary>The skill contains a script.</summary>
    public const string ContainsScript = "SF2001";

    /// <summary>The skill contains an external URL.</summary>
    public const string ContainsExternalUrl = "SF2002";

    /// <summary>The skill contains a binary file.</summary>
    public const string ContainsBinaryFile = "SF2003";

    /// <summary>The skill contains an <c>evals</c> folder.</summary>
    public const string ContainsEvals = "SF2004";

    /// <summary>
    /// The description claims the skill applies always, or to everything — an activation scope so broad that an
    /// agent has nothing to match it against.
    /// </summary>
    public const string ActivationTooBroad = "SF3001";

    /// <summary>
    /// The skill's text pushes an agent to prefer it over its other instructions rather than describing when it
    /// applies.
    /// </summary>
    public const string ActivationManipulation = "SF3002";

    /// <summary>
    /// The body's prose tells the agent to set aside or override instructions it was given — the shape prompt
    /// injection takes when it arrives inside a skill rather than inside user input.
    /// </summary>
    public const string BodyInstructionOverride = "SF4001";

    /// <summary>
    /// The body's prose tells the agent to keep something from the person it is working for.
    /// </summary>
    public const string BodyConcealmentInstruction = "SF4002";

    /// <summary>
    /// The skill fetches something remote from a reference that can change — a branch, a "latest" tag, a
    /// latest-release URL — so running it twice is not guaranteed to run the same thing twice.
    /// </summary>
    public const string MutableRemoteReference = "SF5001";

    /// <summary>
    /// The skill's reach grew while its declared version stayed the same, so a consumer pinned to that version
    /// received the change without being told. Reported by <c>diff</c>, which is the only command that can see it.
    /// </summary>
    public const string VersionSilentAboutGrowth = "SF6001";

    /// <summary>
    /// The skill declares compatibility with a provider SkillForge does not recognise, so nothing was checked
    /// against it. Usually a spelling of a known identifier; sometimes a provider SkillForge has not learned yet.
    /// </summary>
    public const string ProviderUnknown = "SF7001";

    /// <summary>
    /// The <c>name</c> is longer than a provider the skill declares compatibility with accepts.
    /// </summary>
    public const string ProviderNameTooLong = "SF7002";

    /// <summary>
    /// The <c>description</c> is longer than a provider the skill declares compatibility with accepts.
    /// </summary>
    public const string ProviderDescriptionTooLong = "SF7003";
}
