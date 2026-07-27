using System.CommandLine;
using SkillForge.Application.Abstractions;

namespace SkillForge.Cli;

/// <summary>
/// The options every command shares, read back out of a parse result in one place.
/// </summary>
internal sealed record GlobalOptions(Option<bool> Quiet, Option<bool> Verbose, Option<bool> NoColor)
{
    internal ReportRenderOptions Read(ParseResult parseResult) =>
        new(
            Quiet: parseResult.GetValue(Quiet),
            Verbose: parseResult.GetValue(Verbose),
            NoColor: parseResult.GetValue(NoColor) || IsColourDisabledByEnvironment());

    /// <summary>
    /// Honours the <c>NO_COLOR</c> convention, so CI logs are clean without anyone passing a flag.
    /// </summary>
    private static bool IsColourDisabledByEnvironment() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
}
