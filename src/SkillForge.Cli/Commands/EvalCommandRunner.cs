using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Evaluation;
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

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="validator">Validates it, so a case can pin or forbid diagnostic codes.</param>
    /// <param name="cases">Reads the eval cases.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="renderer">Reports a skill that could not be loaded.</param>
    public EvalCommandRunner(
        ISkillLoader loader,
        ISkillValidator validator,
        IEvalCaseReader cases,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);

        _loader = loader;
        _validator = validator;
        _cases = cases;
        _fileSystem = fileSystem;
        _renderer = renderer;
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

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(report, read.Diagnostics)
            : ToText(report, read.Diagnostics);

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

        return report.Passed ? ExitCodes.Success : ExitCodes.ValidationFailed;
    }

    private static string ToText(EvalReport report, IReadOnlyList<Domain.Diagnostics.Diagnostic> readProblems)
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
                builder.AppendLine($"- {result.Name}  (asserts nothing)");
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

        return builder.ToString();
    }

    private static string ToJson(EvalReport report, IReadOnlyList<Domain.Diagnostics.Diagnostic> readProblems)
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
