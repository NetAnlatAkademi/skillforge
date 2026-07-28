namespace SkillForge.Domain.Evaluation;

/// <summary>
/// One thing a skill's author claims should stay true about it.
/// </summary>
/// <param name="Name">What the case is called, shown in the report.</param>
/// <param name="RequiredFiles">Files that must exist in the skill.</param>
/// <param name="RequiresShellPermission">
/// Whether the skill must declare a shell permission. <see langword="null"/> means the case says nothing about it.
/// </param>
/// <param name="ForbiddenDiagnostics">Diagnostic codes that must not appear when the skill is validated.</param>
/// <param name="ExpectedDiagnostics">
/// Diagnostic codes that must appear. Present so a skill can pin a finding it has deliberately accepted, rather
/// than being forced to fix it to keep its evals green.
/// </param>
/// <param name="DescriptionMentions">Terms the description must contain.</param>
/// <param name="Activation">A prompt and whether its wording should overlap the description.</param>
/// <param name="ModelActivation">
/// Prompts the skill should and should not be chosen for, checked by asking a model. Only runs when the caller supplies
/// a model explicitly; a case carrying one is reported as skipped otherwise, never as passed.
/// </param>
public sealed record EvalCase(
    string Name,
    IReadOnlyList<string> RequiredFiles,
    bool? RequiresShellPermission,
    IReadOnlyList<string> ForbiddenDiagnostics,
    IReadOnlyList<string> ExpectedDiagnostics,
    IReadOnlyList<string> DescriptionMentions,
    ActivationExpectation? Activation,
    ModelActivationExpectation? ModelActivation = null)
{
    /// <summary>Gets a case that asserts nothing, used as a starting point when reading a file.</summary>
    public static EvalCase Empty(string name) => new(name, [], null, [], [], [], null);

    /// <summary>Gets a value indicating whether the case asserts anything at all.</summary>
    /// <remarks>
    /// A case that asserts nothing passes trivially, which is worse than useless: it makes a suite look bigger than
    /// it is. The runner reports it rather than counting it as a pass.
    /// </remarks>
    public bool AssertsSomething =>
        RequiredFiles.Count > 0
        || RequiresShellPermission is not null
        || ForbiddenDiagnostics.Count > 0
        || ExpectedDiagnostics.Count > 0
        || DescriptionMentions.Count > 0
        || Activation is not null
        || ModelActivation is not null;
}
