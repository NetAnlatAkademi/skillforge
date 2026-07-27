using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// The shell constructs SkillForge points out, and why each one is worth a look.
/// </summary>
/// <remarks>
/// These are **signals, not verdicts** (ADR-006). Every one of them has legitimate uses — a build script may well
/// need <c>sudo</c>, and <c>rm -rf</c> on a temporary directory is ordinary housekeeping. What they have in common
/// is that a reader deciding whether to trust a skill would want to know they are there, and reading every script
/// by hand is exactly what nobody does.
///
/// The list is deliberately short and literal. A pattern that tries to be clever about intent produces false
/// confidence in both directions: it misses the obfuscated case and cries wolf about the ordinary one.
/// </remarks>
public static partial class ShellPrivilegePatterns
{
    /// <summary>Every pattern, in the order findings are reported.</summary>
    public static IReadOnlyList<ShellPrivilegePattern> All { get; } =
    [
        new(
            "a piped installer",
            PipedInstaller(),
            "downloading a script and executing it in one step means the content is never reviewed, and what "
                + "arrives can differ from what was reviewed before"),
        new(
            "recursive force delete",
            RecursiveDelete(),
            "worth checking what it points at, and whether that path can ever be empty or relative"),
        new(
            "dynamic code execution",
            DynamicExecution(),
            "code assembled at runtime cannot be reviewed by reading the script"),
        new(
            "world-writable permissions",
            WorldWritable(),
            "anything on the machine can then modify the file, including between review and execution"),
        new(
            "privilege elevation",
            PrivilegeElevation(),
            "the script asks for rights beyond the agent's own"),
        new(
            "a privileged container",
            PrivilegedContainer(),
            "a privileged container is not isolated from the host in the way an unprivileged one is"),
        new(
            "an encoded command",
            EncodedCommand(),
            "an encoded command hides what will run from anyone reading the script"),
    ];

    [GeneratedRegex(@"\b(curl|wget|iwr|Invoke-WebRequest)\b[^\r\n|]*\|\s*(sudo\s+)?(ba|z|k)?sh\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PipedInstaller();

    [GeneratedRegex(@"\brm\s+(-[a-zA-Z]*\s+)*-[a-zA-Z]*[rR][a-zA-Z]*f|\brm\s+(-[a-zA-Z]*\s+)*-[a-zA-Z]*f[a-zA-Z]*[rR]",
        RegexOptions.CultureInvariant)]
    private static partial Regex RecursiveDelete();

    [GeneratedRegex(@"\b(Invoke-Expression|iex)\s|\beval\s*[\(""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicExecution();

    [GeneratedRegex(@"\bchmod\s+(-[a-zA-Z]+\s+)*777\b", RegexOptions.CultureInvariant)]
    private static partial Regex WorldWritable();

    [GeneratedRegex(@"(^|[\s;&|])sudo\s", RegexOptions.CultureInvariant)]
    private static partial Regex PrivilegeElevation();

    [GeneratedRegex(@"docker\s+run\b[^\r\n]*--privileged", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivilegedContainer();

    [GeneratedRegex(@"-Enc(odedCommand)?\s+[A-Za-z0-9+/=]{16,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EncodedCommand();
}
