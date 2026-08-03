namespace SkillForge.Application.Abstractions;

/// <summary>
/// How output should be presented.
/// </summary>
/// <param name="Quiet">Show only the verdict and any errors.</param>
/// <param name="Verbose">Show the checks that passed as well as the findings.</param>
/// <param name="NoColor">Suppress colour and other ANSI output, for logs and pipes.</param>
public sealed record ReportRenderOptions(bool Quiet = false, bool Verbose = false, bool NoColor = false)
{
    /// <summary>The heading a console report is printed under.</summary>
    /// <remarks>
    /// A command that reports findings in the validation shape is not necessarily <c>validate</c>: a policy run
    /// uses the same report, and printing "SkillForge Validate" over it would tell the reader the wrong thing
    /// about what was checked.
    /// </remarks>
    public string Title { get; init; } = "SkillForge Validate";
}
