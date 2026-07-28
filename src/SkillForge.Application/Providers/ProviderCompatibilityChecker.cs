using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Providers;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Providers;

/// <summary>
/// Default <see cref="IProviderCompatibilityChecker"/>.
/// </summary>
/// <remarks>
/// Not an <c>ISkillValidationRule</c>, for one reason: a rule sees the skill and nothing else, and these checks
/// also depend on what the run asked for (<c>--provider</c>). Threading run options through every rule to serve
/// three of them would be the wrong trade. The findings are merged into the report the same way the loader's are,
/// so suppression, ordering, JSON and SARIF all apply unchanged.
///
/// Nothing is checked against a provider the skill does not name. A rule that judged every skill against every
/// provider SkillForge knows would fire constantly on skills that never claimed to be portable, and the project's
/// measurement discipline exists to stop exactly that.
/// </remarks>
public sealed class ProviderCompatibilityChecker : IProviderCompatibilityChecker
{
    private readonly IAgentProviderRegistry _registry;

    /// <summary>Initialises the checker.</summary>
    /// <param name="registry">The providers SkillForge recognises.</param>
    public ProviderCompatibilityChecker(IAgentProviderRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public IReadOnlyList<Diagnostic> Check(SkillDefinition skill, IReadOnlyList<string> additionalProviders)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(additionalProviders);

        var diagnostics = new List<Diagnostic>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, wasDeclared) in Requested(skill, additionalProviders))
        {
            if (id.Length == 0 || !seen.Add(id))
            {
                continue;
            }

            var profile = _registry.Find(id);

            if (profile is null)
            {
                diagnostics.Add(UnknownProvider(id, wasDeclared, skill));
                continue;
            }

            diagnostics.AddRange(CheckLimits(skill, profile));
        }

        return diagnostics;
    }

    /// <summary>
    /// The providers to consider: the skill's own declarations first, in the order it lists them, then the ones
    /// the caller asked about.
    /// </summary>
    private static IEnumerable<(string Id, bool WasDeclared)> Requested(
        SkillDefinition skill,
        IReadOnlyList<string> additionalProviders) =>
        skill.Frontmatter.Compatibility
            .Select(id => (Id: id.Trim(), WasDeclared: true))
            .Concat(additionalProviders.Select(id => (Id: id.Trim(), WasDeclared: false)));

    private Diagnostic UnknownProvider(string id, bool wasDeclared, SkillDefinition skill)
    {
        var suggestion = _registry.Suggest(id);
        var known = string.Join(", ", _registry.Profiles.Select(profile => profile.Id));

        var where = wasDeclared
            ? $"Compatibility is declared with '{id}'"
            : $"'{id}' was asked about with --provider";

        return Diagnostic.Warning(
            DiagnosticCodes.ProviderUnknown,
            $"{where}, which SkillForge does not recognise, so nothing was checked against it.",
            SkillDefinition.SkillFileName,
            skill.Frontmatter.StartLine,
            suggestion is null
                ? $"SkillForge knows: {known}. An identifier outside that list is not wrong — it only means "
                    + "SkillForge has no profile for it yet."
                : $"'{suggestion}' is the known identifier this is closest to.",
            suggestion is null || !wasDeclared
                ? null
                : $"in 'compatibility', replace '{id}' with '{suggestion}'");
    }

    /// <summary>
    /// Compares the skill against the limits the profile actually declares. A profile with none produces
    /// nothing — see <see cref="AgentProviderProfile"/> on why an unknown limit is not a missing one.
    /// </summary>
    private static IEnumerable<Diagnostic> CheckLimits(SkillDefinition skill, AgentProviderProfile profile)
    {
        if (profile.NameMaxLength is { } nameLimit && skill.Name.Length > nameLimit)
        {
            yield return OverLimit(
                DiagnosticCodes.ProviderNameTooLong,
                "name",
                skill.Name.Length,
                nameLimit,
                profile,
                skill);
        }

        if (profile.DescriptionMaxLength is { } descriptionLimit && skill.Description.Length > descriptionLimit)
        {
            yield return OverLimit(
                DiagnosticCodes.ProviderDescriptionTooLong,
                "description",
                skill.Description.Length,
                descriptionLimit,
                profile,
                skill);
        }
    }

    private static Diagnostic OverLimit(
        string code,
        string field,
        int actual,
        int limit,
        AgentProviderProfile profile,
        SkillDefinition skill)
    {
        var source = profile.DocumentationUrl is null
            ? string.Empty
            : $" The limit is documented at {profile.DocumentationUrl}.";

        return Diagnostic.Warning(
            code,
            $"The {field} is {actual} characters; {profile.DisplayName} accepts at most {limit}.",
            SkillDefinition.SkillFileName,
            skill.Frontmatter.StartLine,
            $"Shorten the {field}, or stop declaring compatibility with {profile.DisplayName}.{source}");
    }
}
