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

    /// <summary>What the entries in a batch report should be called.</summary>
    /// <remarks>
    /// Same reason as <see cref="Title"/>, one level down. A <c>policy check</c> run holds the policy file and any
    /// MCP configurations beside the skills, and counting an MCP configuration as a skill states something untrue
    /// about what was read. Commands that really do report on skills leave this alone.
    /// </remarks>
    public string SubjectPlural { get; init; } = "Skills";
}
