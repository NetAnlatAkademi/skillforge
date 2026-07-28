using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Evaluation;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Evaluation;

/// <summary>
/// Checks a skill against the claims its author wrote down.
/// </summary>
/// <remarks>
/// A pure function of a loaded skill and its validation report, so the whole of `eval` is testable without touching a
/// disk. Nothing here runs a model, sends a request, or executes a script — an eval asks whether the skill still
/// looks the way its author said it should, which is a regression question rather than a behavioural one.
///
/// That boundary is deliberate and it is the honest limit of a deterministic evaluator. See
/// <see cref="ActivationExpectation"/> for why the activation check reports vocabulary overlap and refuses to call
/// it activation.
/// </remarks>
public static class EvalRunner
{
    /// <summary>
    /// Runs a skill's eval cases.
    /// </summary>
    /// <param name="skill">The loaded skill.</param>
    /// <param name="report">The skill's validation report, so a case can pin or forbid diagnostic codes.</param>
    /// <param name="cases">The cases to run.</param>
    /// <returns>What held and what did not.</returns>
    public static EvalReport Run(SkillDefinition skill, ValidationReport report, IReadOnlyList<EvalCase> cases)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(cases);

        return new EvalReport(
            skill.Name,
            skill.DirectoryPath,
            [.. cases.Select(evalCase => RunCase(skill, report, evalCase))]);
    }

    /// <summary>
    /// Whether a case declares a model activation expectation and nothing this evaluator can answer.
    /// </summary>
    private static bool OnlyAssertsModelActivation(EvalCase evalCase) =>
        evalCase.ModelActivation is not null
        && evalCase.RequiredFiles.Count == 0
        && evalCase.RequiresShellPermission is null
        && evalCase.ForbiddenDiagnostics.Count == 0
        && evalCase.ExpectedDiagnostics.Count == 0
        && evalCase.DescriptionMentions.Count == 0
        && evalCase.Activation is null;

    private static EvalCaseResult RunCase(SkillDefinition skill, ValidationReport report, EvalCase evalCase)
    {
        if (!evalCase.AssertsSomething)
        {
            return new EvalCaseResult(evalCase.Name, [], Skipped: true);
        }

        // A case whose only assertion is model_activation has nothing for a deterministic run to check, and reporting
        // it as passed would be the exact lie this evaluator exists to avoid — it would say a model routed correctly
        // when no model was asked. It is skipped here and answered in the model activation section instead.
        if (OnlyAssertsModelActivation(evalCase))
        {
            return new EvalCaseResult(
                evalCase.Name,
                [],
                Skipped: true,
                SkipReason: "answered in the model activation section, which needs a model");
        }

        var assertions = new List<EvalAssertion>();

        foreach (var required in evalCase.RequiredFiles)
        {
            var present = skill.Resources.Any(resource =>
                string.Equals(resource.RelativePath, required, StringComparison.Ordinal));

            assertions.Add(new EvalAssertion(
                $"ships {required}",
                present,
                present ? null : "the skill does not contain it"));
        }

        if (evalCase.RequiresShellPermission is { } requiresShell)
        {
            var declares = skill.Configuration.DeclaresShellPermission;

            assertions.Add(new EvalAssertion(
                requiresShell ? "declares a shell permission" : "declares no shell permission",
                declares == requiresShell,
                declares == requiresShell
                    ? null
                    : declares
                        ? "skillforge.yaml declares one"
                        : "skillforge.yaml declares none"));
        }

        foreach (var code in evalCase.ForbiddenDiagnostics)
        {
            var found = Findings(report, code);

            assertions.Add(new EvalAssertion(
                $"reports no {code}",
                found.Count == 0,
                found.Count == 0 ? null : Locations(found)));
        }

        foreach (var code in evalCase.ExpectedDiagnostics)
        {
            var found = Findings(report, code);

            assertions.Add(new EvalAssertion(
                $"still reports {code}",
                found.Count > 0,
                found.Count > 0 ? Locations(found) : "it is no longer reported"));
        }

        foreach (var term in evalCase.DescriptionMentions)
        {
            var mentioned = skill.Description.Contains(term, StringComparison.OrdinalIgnoreCase);

            assertions.Add(new EvalAssertion(
                $"the description mentions '{term}'",
                mentioned,
                mentioned ? null : "it does not"));
        }

        if (evalCase.Activation is { } activation)
        {
            assertions.Add(CheckVocabulary(skill.Description, activation));
        }

        return new EvalCaseResult(evalCase.Name, assertions);
    }

    /// <summary>
    /// Reports whether a prompt and a description share vocabulary — and says only that.
    /// </summary>
    /// <remarks>
    /// Phrased as vocabulary throughout, in the assertion text as well as here, because the moment this is described
    /// as "the skill activates" it is claiming something it cannot know. See <see cref="ActivationExpectation"/>.
    /// </remarks>
    private static EvalAssertion CheckVocabulary(string description, ActivationExpectation activation)
    {
        var shared = SharedTerms(activation.Prompt, description);
        var overlaps = shared.Count > 0;
        var passed = overlaps == activation.ExpectOverlap;

        var claim = activation.ExpectOverlap
            ? $"the description shares wording with \"{activation.Prompt}\""
            : $"the description shares no wording with \"{activation.Prompt}\"";

        var detail = overlaps
            ? $"shared: {string.Join(", ", shared)}"
            : "nothing in common";

        return new EvalAssertion(claim, passed, detail);
    }

    /// <summary>
    /// The meaningful words a prompt and a description have in common.
    /// </summary>
    /// <remarks>
    /// A length filter alone was tried first and does not work. "Use this skill when tuning a database index" and
    /// "translate this paragraph into Turkish" share **"this"** — four characters, so it survives any sane length
    /// bar — and that one word was enough to make two entirely unrelated sentences look related. A stop-word list is
    /// therefore unavoidable rather than optional.
    ///
    /// The list below is **English only**, and that is a real limitation worth stating rather than hiding: a Turkish
    /// or German description will share function words this check does not know about, and its overlap result will be
    /// correspondingly generous. It is another reason the report describes shared vocabulary instead of predicting
    /// behaviour.
    ///
    /// Still crude on purpose. A cleverer similarity score would invite a reader to treat the number as a prediction,
    /// which is exactly what this cannot be.
    /// </remarks>
    private static IReadOnlyList<string> SharedTerms(string prompt, string description) =>
        [.. Terms(prompt)
            .Intersect(Terms(description), StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Common English function words. Long enough to pass a length filter, empty enough to carry no signal.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "this", "that", "these", "those", "with", "from", "into", "your", "yours", "their", "have", "has",
        "been", "being", "when", "whenever", "while", "which", "what", "where", "will", "would", "should",
        "could", "must", "then", "than", "there", "here", "some", "any", "each", "every", "also", "just",
        "only", "very", "more", "most", "much", "many", "such", "over", "under", "about", "after", "before",
        "does", "done", "make", "made", "used", "using", "need", "needs", "want", "like", "them", "they",
        "you", "and", "but", "not", "for", "the", "are", "was", "were", "its", "it's", "please", "help",
    };

    private static HashSet<string> Terms(string text) =>
        [.. text
            .Split([' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\'', '/', '\\'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length > 3 && !StopWords.Contains(word))];

    private static IReadOnlyList<Diagnostic> Findings(ValidationReport report, string code) =>
        [.. report.Diagnostics.Where(diagnostic =>
            string.Equals(diagnostic.Code, code, StringComparison.OrdinalIgnoreCase))];

    private static string Locations(IReadOnlyList<Diagnostic> found) =>
        string.Join(", ", found.Select(diagnostic =>
            diagnostic.Line is { } line ? $"{diagnostic.FilePath}:{line}" : diagnostic.FilePath ?? "the skill"));
}
