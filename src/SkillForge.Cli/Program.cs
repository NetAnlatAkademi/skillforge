using System.CommandLine;

namespace SkillForge.Cli;

/// <summary>
/// Entry point of the SkillForge CLI.
/// </summary>
/// <remarks>
/// Two jobs only: build the object graph, and turn whatever happens into an exit code. Expected failures
/// already arrive as diagnostics, so the catch here is for the unexpected — and it prints something a user
/// can act on rather than a raw stack trace.
/// </remarks>
internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            await using var services = CompositionRoot.Build();
            var root = SkillForgeCommandLine.Build(services);

            var parseResult = root.Parse(args);

            // System.CommandLine reports its own usage errors; SkillForge maps them to exit code 2 so a CI
            // step can tell "you typed the command wrong" apart from "the skill is invalid".
            if (parseResult.Errors.Count > 0)
            {
                foreach (var error in parseResult.Errors)
                {
                    await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
                }

                await Console.Error.WriteLineAsync(
                    "Run 'skillforge --help' to see the available commands.").ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }

            return await parseResult.InvokeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Cancelled.").ConfigureAwait(false);
            return ExitCodes.UnexpectedError;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"SkillForge could not read the skill: {exception.Message}")
                .ConfigureAwait(false);
            return ExitCodes.UnexpectedError;
        }
    }
}
