namespace SkillForge.Cli;

/// <summary>
/// Process exit codes. Part of the public contract: CI steps branch on these.
/// </summary>
internal static class ExitCodes
{
    /// <summary>The command succeeded and nothing blocking was found.</summary>
    internal const int Success = 0;

    /// <summary>A validation error was found, or a warning while <c>--strict</c> was in effect.</summary>
    internal const int ValidationFailed = 1;

    /// <summary>The command line itself was wrong: unknown option, missing argument.</summary>
    internal const int InvalidUsage = 2;

    /// <summary>Something unexpected went wrong inside SkillForge.</summary>
    internal const int UnexpectedError = 3;
}
