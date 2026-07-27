namespace SkillForge.Application.Abstractions;

/// <summary>
/// How output should be presented.
/// </summary>
/// <param name="Quiet">Show only the verdict and any errors.</param>
/// <param name="Verbose">Show the checks that passed as well as the findings.</param>
/// <param name="NoColor">Suppress colour and other ANSI output, for logs and pipes.</param>
public sealed record ReportRenderOptions(bool Quiet = false, bool Verbose = false, bool NoColor = false);
