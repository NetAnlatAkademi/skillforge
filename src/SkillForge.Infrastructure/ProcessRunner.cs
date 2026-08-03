using System.Diagnostics;
using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure;

/// <summary>
/// Runs an external program with <see cref="Process"/>.
/// </summary>
/// <remarks>
/// Arguments go through <see cref="ProcessStartInfo.ArgumentList"/> rather than a single command line, so nothing
/// has to be quoted and a path containing a space or a quote cannot turn into a second argument. No shell is
/// involved, so nothing in an argument can be interpreted as a command.
///
/// Both streams are read while the process runs. Waiting for exit first and reading afterwards deadlocks as soon
/// as a program writes more than the pipe buffer holds, which git does the moment a repository has any size.
/// </remarks>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async ValueTask<ProcessResult?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or PlatformNotSupportedException)
        {
            // The program is not installed, or the platform will not run it. "Could not ask" is a real answer and
            // the caller distinguishes it from an answer of "no".
            return null;
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessResult(
            process.ExitCode,
            (await standardOutput.ConfigureAwait(false)).TrimEnd(),
            (await standardError.ConfigureAwait(false)).TrimEnd());
    }
}
