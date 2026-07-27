using SkillForge.Application.Abstractions;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Sends a report wherever the user asked for it: the console, stdout, or a file.
/// </summary>
/// <remarks>
/// Machine-readable output goes to stdout by default so it can be piped, and to a file when
/// <c>--output</c> is given. When it goes to a file the console still gets the human-readable report, so a
/// CI log is not silent about what happened.
/// </remarks>
internal sealed class ReportOutput
{
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    public ReportOutput(
        IFileSystem fileSystem,
        IValidationReportRenderer renderer,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(serializers);

        _fileSystem = fileSystem;
        _renderer = renderer;
        _serializers = serializers.ToArray();
    }

    /// <summary>
    /// Writes a report in the requested format.
    /// </summary>
    /// <param name="report">Report to present.</param>
    /// <param name="format">One of <see cref="OutputFormat"/>.</param>
    /// <param name="outputPath">File to write to, or <see langword="null"/> for stdout.</param>
    /// <param name="renderOptions">How to present console output.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the report has been written.</returns>
    internal async Task WriteAsync(
        ValidationReport report,
        string format,
        string? outputPath,
        ReportRenderOptions renderOptions,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, OutputFormat.Console, StringComparison.OrdinalIgnoreCase))
        {
            _renderer.Render(report, renderOptions);
            return;
        }

        var serializer = _serializers.Single(candidate =>
            string.Equals(candidate.Format, format, StringComparison.OrdinalIgnoreCase));

        var text = serializer.Serialize(report);

        if (outputPath is null)
        {
            await Console.Out.WriteAsync(text).ConfigureAwait(false);
            return;
        }

        var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
        if (directory is { Length: > 0 })
        {
            _fileSystem.CreateDirectory(directory);
        }

        await _fileSystem.WriteAllTextAsync(outputPath, text, cancellationToken).ConfigureAwait(false);

        // The file is for machines; the person watching the build still needs to see the outcome.
        _renderer.Render(report, renderOptions);
    }
}
