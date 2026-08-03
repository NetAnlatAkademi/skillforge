namespace SkillForge.Application.Abstractions;

/// <summary>
/// Runs an external program and reports what it said.
/// </summary>
/// <remarks>
/// The seam that keeps <c>git</c> out of the application layer. SkillForge shells out to git rather than linking a
/// git library: the questions it asks are three read-only ones, and a native dependency to ask them would be a
/// larger commitment than the answers are worth.
///
/// Implementations never throw for a non-zero exit code — a program that failed is an answer, and the caller
/// decides what it means. They throw only when the program could not be started at all.
/// </remarks>
public interface IProcessRunner
{
    /// <summary>Runs a program to completion.</summary>
    /// <param name="fileName">Program to run, resolved through <c>PATH</c>.</param>
    /// <param name="arguments">Arguments, passed individually so nothing has to be quoted or escaped.</param>
    /// <param name="workingDirectory">Directory to run in.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns>
    /// What the program said, or <see langword="null"/> when it could not be started — which is the honest answer
    /// when git is not installed, and is not the same as git answering "no".
    /// </returns>
    ValueTask<ProcessResult?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}

/// <summary>What a program said.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="StandardOutput">Everything written to stdout, with trailing whitespace trimmed.</param>
/// <param name="StandardError">Everything written to stderr, with trailing whitespace trimmed.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets a value indicating whether the program succeeded.</summary>
    public bool Succeeded => ExitCode == 0;
}
