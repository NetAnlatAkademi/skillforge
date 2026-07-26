namespace SkillForge.Cli.Commands;

/// <summary>
/// How a command should present its result.
/// </summary>
internal static class OutputFormat
{
    /// <summary>Human-readable console output.</summary>
    internal const string Console = "console";

    /// <summary>SkillForge's own JSON report.</summary>
    internal const string Json = "json";

    /// <summary>SARIF 2.1.0, for GitHub code scanning.</summary>
    internal const string Sarif = "sarif";

    /// <summary>Every accepted value, for validation and help text.</summary>
    internal static IReadOnlyList<string> All { get; } = [Console, Json, Sarif];
}
