using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation.Rules;

namespace SkillForge.Application.Validation;

/// <summary>
/// The set of rules SkillForge runs by default.
/// </summary>
/// <remarks>
/// An explicit list rather than assembly scanning: reflection would make the active rule set depend on what happens
/// to be loaded, and a rule silently disappearing is worse than one that has to be added here by hand.
/// </remarks>
public static class SkillValidationRules
{
    /// <summary>
    /// Creates the default rule set.
    /// </summary>
    /// <param name="fileSystem">
    /// Needed by the one rule that reads the skill's scripts. Passed in rather than resolved here so this stays a
    /// plain list with no container behind it.
    /// </param>
    /// <returns>Every rule SkillForge ships, in no particular order.</returns>
    public static IReadOnlyList<ISkillValidationRule> CreateDefault(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        return
        [
            new NameRequiredRule(),
            new DescriptionRequiredRule(),
            new NameFormatRule(),
            new ReferencedFileExistsRule(),
            new ReferenceLeavesSkillRule(),
            new ReferenceEscapesCollectionRule(),
            new PackageVersionRule(),
            new DescriptionLengthRule(),
            new DescriptionActivationRule(),
            new SkillFileLengthRule(),
            new LicenseDeclaredRule(),
            new CompatibilityDeclaredRule(),
            new NetworkDeclarationRule(),
            new ScriptPermissionRule(),
            new ShellPrivilegeRule(fileSystem),
            new OverBroadActivationRule(),
            new ActivationManipulationRule(),
            new InstructionOverrideRule(),
            new ConcealmentRule(),
            new MutableRemoteReferenceRule(fileSystem),
        ];
    }
}
