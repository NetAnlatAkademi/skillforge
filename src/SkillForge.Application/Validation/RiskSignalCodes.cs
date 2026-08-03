using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation;

/// <summary>
/// The codes a security-focused run reports, and nothing else.
/// </summary>
/// <remarks>
/// <c>scan</c> runs the same rules <c>validate</c> runs — there is no separate scanner, and a second engine would be
/// a second set of bugs. What it changes is the report: a missing license or a short description is a quality
/// finding, and burying a prompt-injection signal underneath twelve of those is how a signal gets ignored.
///
/// An explicit list rather than a band prefix, because the bands do not line up with the question. `SF1xxx` holds
/// both "no license" and "this script runs sudo"; only one of them belongs here.
///
/// The loader's failures are included on purpose. "This skill could not be read" is the one answer a scan must never
/// present as "nothing found".
/// </remarks>
public static class RiskSignalCodes
{
    /// <summary>Gets the codes a scan reports.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // A skill that could not be read has not been cleared of anything.
        DiagnosticCodes.SkillFileNotFound,
        DiagnosticCodes.FrontmatterNotFound,
        DiagnosticCodes.FrontmatterNotParsable,
        DiagnosticCodes.DuplicateMetadataField,

        // Reach beyond the skill's own directory.
        DiagnosticCodes.PathEscapesSkillDirectory,
        DiagnosticCodes.ReferenceLeavesSkill,

        // What the skill talks to, runs, and admits to.
        DiagnosticCodes.ExternalUrlPresent,
        DiagnosticCodes.ScriptWithoutDeclaredPermission,
        DiagnosticCodes.BroadShellPrivileges,

        // Activation and instruction risks.
        DiagnosticCodes.ActivationTooBroad,
        DiagnosticCodes.ActivationManipulation,
        DiagnosticCodes.BodyInstructionOverride,
        DiagnosticCodes.BodyConcealmentInstruction,

        // Supply chain.
        DiagnosticCodes.MutableRemoteReference,

        // A configuration or eval file that was ignored may be the one that would have suppressed a finding.
        DiagnosticCodes.ConfigurationNotParsable,
    };

    /// <summary>Determines whether a code is a risk signal.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <returns><see langword="true"/> when a security-focused run should report it.</returns>
    public static bool Includes(string code) => All.Contains(code);
}
