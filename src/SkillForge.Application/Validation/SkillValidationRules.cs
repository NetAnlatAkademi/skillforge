using SkillForge.Application.Validation.Rules;

namespace SkillForge.Application.Validation;

/// <summary>
/// The set of rules SkillForge runs by default.
/// </summary>
/// <remarks>
/// An explicit list rather than assembly scanning: reflection would make the active rule set depend on
/// what happens to be loaded, and a rule silently disappearing is worse than one that has to be added
/// here by hand. Phase 3 registers these with the dependency injection container.
/// </remarks>
public static class SkillValidationRules
{
    /// <summary>
    /// Creates the default rule set.
    /// </summary>
    /// <returns>Every rule SkillForge ships, in no particular order.</returns>
    public static IReadOnlyList<ISkillValidationRule> CreateDefault() =>
    [
        new NameRequiredRule(),
        new DescriptionRequiredRule(),
        new NameFormatRule(),
        new ReferencedFileExistsRule(),
        new ReferencePathContainmentRule(),
        new PackageVersionRule(),
        new DescriptionLengthRule(),
        new DescriptionActivationRule(),
        new SkillFileLengthRule(),
        new LicenseDeclaredRule(),
        new CompatibilityDeclaredRule(),
    ];
}
