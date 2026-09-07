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

    /// <summary>A Mermaid diagram, for a pull request that renders one.</summary>
    internal const string Mermaid = "mermaid";

    /// <summary>
    /// Every value the report commands accept, for validation and help text.
    /// </summary>
    /// <remarks>
    /// <see cref="Mermaid"/> is deliberately not here. It is offered by <c>graph</c> alone, and a format listed as
    /// generally available would have to be implemented by every command that takes <c>--format</c> — including the
    /// ones whose output is a list of findings, which is not a diagram.
    /// </remarks>
    internal static IReadOnlyList<string> All { get; } = [Console, Json, Sarif];
}
