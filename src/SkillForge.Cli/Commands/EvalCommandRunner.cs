using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Evaluation;
using SkillForge.Application.Modeling;
using SkillForge.Application.Validation;
using SkillForge.Domain.Evaluation;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge eval</c> does.
/// </summary>
/// <remarks>
/// An eval asks whether a skill still looks the way its author said it should. It is a regression harness, not a
/// behavioural one: nothing here runs a model, sends a request or executes a script, and the output never claims a
/// skill "would" or "would not" be chosen by an agent. The activation cases report shared vocabulary and say so in
/// those words — see <see cref="ActivationExpectation"/> for why that limit is real rather than temporary.
///
/// A skill with no <c>evals</c> folder is not a failure and not a pass: it exits zero with a line saying there is
/// nothing to run. A skill with an empty suite **is** a failure, because an author who created the folder and wrote
/// no cases should not be told everything is fine.
/// </remarks>
internal sealed class EvalCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISkillLoader _loader;
    private readonly ISkillValidator _validator;
    private readonly IEvalCaseReader _cases;
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;
    private readonly IModelRunnerFactory _modelRunners;
    private readonly SkillCatalogue _catalogue;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="validator">Validates it, so a case can pin or forbid diagnostic codes.</param>
    /// <param name="cases">Reads the eval cases.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="renderer">Reports a skill that could not be loaded.</param>
    /// <param name="modelRunners">
    /// Creates the model runner for <c>model_activation</c> cases. Registered always, used only when the caller names a
    /// model, and it opens no connection until it is asked a question.
    /// </param>
    /// <param name="catalogue">Finds the sibling skills a probe offers as distractors.</param>
    public EvalCommandRunner(
        ISkillLoader loader,
        ISkillValidator validator,
        IEvalCaseReader cases,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer,
        IModelRunnerFactory modelRunners,
        SkillCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(modelRunners);
        ArgumentNullException.ThrowIfNull(catalogue);

        _loader = loader;
        _validator = validator;
        _cases = cases;
        _fileSystem = fileSystem;
        _renderer = renderer;
        _modelRunners = modelRunners;
        _catalogue = catalogue;
    }

    /// <summary>Runs a skill's evals and prints the result.</summary>
    /// <param name="request">What to evaluate and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code: 0 when every case held, 1 when one did not.</returns>
    internal async Task<int> RunAsync(EvalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await _loader.LoadAsync(request.Path, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(request.Path, load.Diagnostics),
                request.RenderOptions);

            return ExitCodes.ValidationFailed;
        }

        var skill = load.Value;

        var read = await _cases.ReadAsync(skill.DirectoryPath, cancellationToken).ConfigureAwait(false);
        var cases = read.Value ?? [];

        // No evals folder at all is a different answer from an empty one, and conflating them would either nag every
        // skill that has not adopted evals or bless every author who created the folder and stopped there.
        if (cases.Count == 0 && read.Diagnostics.Count == 0)
        {
            await Console.Out
                .WriteLineAsync($"No evals found for {skill.Name}. Add cases under evals/ to use this command.")
                .ConfigureAwait(false);

            return ExitCodes.Success;
        }

        var validation = await _validator.ValidateAsync(skill, cancellationToken).ConfigureAwait(false);
        var report = EvalRunner.Run(skill, validation, cases);

        var expectation = MergedModelActivation(cases);

        if (expectation is not null && request.Model is null)
        {
            // Skipped, and said out loud. Reporting it as passed would be the worst of the three options, and leaving
            // it out entirely would hide a case the author wrote.
            await Console.Out.WriteLineAsync(
                $"Skipped {expectation.PromptCount} model activation prompt(s): no model was given. "
                + "Pass --model and --model-endpoint to run them.").ConfigureAwait(false);
        }

        ModelActivationReport? activation = null;

        if (expectation is not null && request.Model is not null)
        {
            var requests = expectation.RequestCount;

            if (requests > request.MaxModelRequests)
            {
                await Console.Error.WriteLineAsync(
                    $"These model activation cases need {requests} model requests, over the limit of "
                    + $"{request.MaxModelRequests}. Raise --max-model-requests, or lower 'runs' in the eval file.")
                    .ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }

            try
            {
                var runner = _modelRunners.Create(request.Model);
                var distractors = await _catalogue
                    .DistractorsAsync(skill, cancellationToken)
                    .ConfigureAwait(false);

                activation = await new ActivationProber(runner)
                    .ProbeAsync(skill, distractors, expectation, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ModelRunnerException exception)
            {
                // An unreachable model is not a fact about the skill. Reporting "did not fire" here would be a lie
                // dressed as evidence, so the run fails as a usage problem and says which endpoint.
                await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }
        }

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(report, read.Diagnostics, activation)
            : ToText(report, read.Diagnostics, activation);

        if (request.OutputPath is { Length: > 0 } outputPath)
        {
            var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
            if (directory is { Length: > 0 })
            {
                _fileSystem.CreateDirectory(directory);
            }

            await _fileSystem.WriteAllTextAsync(outputPath, text, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Console.Out.WriteAsync(text).ConfigureAwait(false);
        }

        // A model result can fail the run, but only when the author declared what they expected of it — the threshold
        // is theirs, so the verdict is theirs too.
        var modelFailed = activation is { Outcomes.Count: > 0 } and { AllMet: false };

        return report.Passed && !modelFailed ? ExitCodes.Success : ExitCodes.ValidationFailed;
    }

    /// <summary>
    /// Collects every <c>model_activation</c> case into one expectation, so the whole file's prompts are probed against
    /// one catalogue and one request budget rather than case by case.
    /// </summary>
    /// <remarks>
    /// Runs and threshold come from the first case that declares them: two cases disagreeing about how many runs to
    /// make is a question the file has to answer, not something to average away silently.
    /// </remarks>
    private static ModelActivationExpectation? MergedModelActivation(IReadOnlyList<EvalCase> cases)
    {
        var declared = cases
            .Select(evalCase => evalCase.ModelActivation)
            .OfType<ModelActivationExpectation>()
            .ToArray();

        if (declared.Length == 0)
        {
            return null;
        }

        return new ModelActivationExpectation(
            [.. declared.SelectMany(expectation => expectation.ShouldFire)],
            [.. declared.SelectMany(expectation => expectation.ShouldNotFire)],
            declared[0].Runs,
            declared[0].Threshold);
    }

    private static string ToText(
        EvalReport report,
        IReadOnlyList<Domain.Diagnostics.Diagnostic> readProblems,
        ModelActivationReport? activation)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Eval");
        builder.AppendLine();
        builder.AppendLine($"Skill: {report.SkillName}");
        builder.AppendLine($"Path:  {report.SkillPath}");
        builder.AppendLine();

        foreach (var problem in readProblems)
        {
            builder.AppendLine($"! {problem.Code} {problem.Message}");
        }

        if (readProblems.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (var result in report.Cases)
        {
            if (result.Skipped)
            {
                builder.AppendLine($"- {result.Name}  ({result.SkipReason ?? "asserts nothing"})");
                continue;
            }

            builder.AppendLine($"{(result.Passed ? "ok" : "FAIL")}  {result.Name}");

            // A passing case prints nothing more; a failing one prints every assertion that did not hold, with what
            // was actually found. "FAIL" on its own is a puzzle, not a report.
            foreach (var assertion in result.Failures)
            {
                builder.AppendLine($"      expected {assertion.Description}");
                if (assertion.Detail is { Length: > 0 } detail)
                {
                    builder.AppendLine($"      but      {detail}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine(report.Cases.Count == 0
            ? "Result: NO CASES — the evals folder declares none, so nothing was checked."
            : $"Result: {(report.Passed ? "PASS" : "FAIL")}");
        builder.AppendLine(
            $"Passed: {report.PassedCount}  Failed: {report.FailedCount}  Skipped: {report.SkippedCount}");

        AppendModelActivation(builder, activation);

        return builder.ToString();
    }

    /// <summary>
    /// Writes the model's answers as their own section, under a heading that names the model.
    /// </summary>
    /// <remarks>
    /// Every line here says <c>k/n</c> rather than pass or fail alone, because 8 of 10 and 10 of 10 both "pass" a 0.8
    /// threshold and an author needs to see which one they have. A probe that ran without distractors is called out:
    /// offered alone, a model picks almost any skill for almost any prompt, so the number is weak evidence and the
    /// reader has to know which kind they are holding.
    /// </remarks>
    private static void AppendModelActivation(StringBuilder builder, ModelActivationReport? activation)
    {
        if (activation is null)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"Model activation — asked {activation.Model.Name} at {activation.Model.Endpoint}");
        builder.AppendLine(activation.HadDistractors
            ? $"Competing against {activation.Distractors.Count} sibling skill(s)."
            : "No sibling skills to compete against, so this is weak evidence: offered alone, a model chooses "
                + "almost any skill for almost any prompt.");
        builder.AppendLine();

        foreach (var outcome in activation.Outcomes)
        {
            var marker = outcome.Met ? "ok  " : "fail";
            var expected = outcome.ExpectedToFire ? "should be chosen" : "should not be chosen";

            builder.AppendLine(
                $"  {marker} {expected}: chosen in {outcome.ChosenRuns}/{outcome.Runs} runs "
                + $"(needs {outcome.Threshold:P0} agreement) — \"{outcome.Prompt}\"");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"{activation.RequestCount} model request(s), {activation.PromptTokens} prompt and "
            + $"{activation.CompletionTokens} completion tokens reported.");
        builder.AppendLine(
            "This is a sample of one model's routing decision, not a guarantee about any agent. It is the closest "
            + "SkillForge gets to testing activation, and it is still not proof.");
    }

    private static JsonObject ToJson(ModelActivationReport activation) =>
        new()
        {
            ["model"] = new JsonObject
            {
                ["name"] = activation.Model.Name,
                ["endpoint"] = activation.Model.Endpoint,
            },
            ["distractors"] = new JsonArray(
                [.. activation.Distractors.Select(name => (JsonNode)JsonValue.Create(name))]),
            ["requestCount"] = activation.RequestCount,
            ["promptTokens"] = activation.PromptTokens,
            ["completionTokens"] = activation.CompletionTokens,
            ["allMet"] = activation.AllMet,
            ["outcomes"] = new JsonArray(
                [.. activation.Outcomes.Select(outcome => (JsonNode)new JsonObject
                {
                    ["prompt"] = outcome.Prompt,
                    ["expectedToFire"] = outcome.ExpectedToFire,
                    ["chosenRuns"] = outcome.ChosenRuns,
                    ["runs"] = outcome.Runs,
                    ["chosenRate"] = outcome.ChosenRate,
                    ["agreementRate"] = outcome.AgreementRate,
                    ["threshold"] = outcome.Threshold,
                    ["met"] = outcome.Met,
                })]),
            ["disclaimer"] = "Generated by the model named above, not computed from the skill's files. A sample of "
                + "one model's routing decision, not a guarantee about any agent.",
        };

    private static string ToJson(
        EvalReport report,
        IReadOnlyList<Domain.Diagnostics.Diagnostic> readProblems,
        ModelActivationReport? activation)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["skill"] = new JsonObject
            {
                ["name"] = report.SkillName,
                ["path"] = report.SkillPath,
            },
            ["passed"] = report.Passed,
            ["summary"] = new JsonObject
            {
                ["passed"] = report.PassedCount,
                ["failed"] = report.FailedCount,
                ["skipped"] = report.SkippedCount,
            },
            ["cases"] = new JsonArray([.. report.Cases.Select(ToJson)]),
            ["problems"] = new JsonArray([.. readProblems.Select(problem => (JsonNode)new JsonObject
            {
                ["code"] = problem.Code,
                ["message"] = problem.Message,
            })]),
            ["disclaimer"] = "A regression check against the author's declared expectations. Activation cases "
                + "report shared vocabulary, not agent behaviour.",
        };

        // A separate key, never mixed into "cases": these results were generated by a model, and a consumer must be
        // able to tell computed facts from sampled ones without reading the disclaimer.
        if (activation is not null)
        {
            document["modelActivation"] = ToJson(activation);
        }

        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    private static JsonNode ToJson(EvalCaseResult result) =>
        new JsonObject
        {
            ["name"] = result.Name,
            ["passed"] = result.Passed,
            ["skipped"] = result.Skipped,
            ["assertions"] = new JsonArray([.. result.Assertions.Select(assertion => (JsonNode)new JsonObject
            {
                ["expected"] = assertion.Description,
                ["passed"] = assertion.Passed,
                ["detail"] = assertion.Detail,
            })]),
        };
}
