using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Provenance;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge provenance</c> does.
/// </summary>
/// <remarks>
/// It answers one question — "where did this come from, and can I check that?" — and answers it with what was
/// observed. Every unknown is printed as <c>unknown</c> rather than filled in, because a provenance report is
/// believed: a guessed publisher is worse than no publisher at all.
///
/// Descriptive, like <c>inspect</c> and <c>inventory</c>. It exits zero even when nothing can be traced anywhere.
/// Whether that is acceptable is a policy decision, and <c>policy check</c> is where those are made.
/// </remarks>
internal sealed class ProvenanceCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ProvenanceInspector _inspector;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="inspector">Reads the provenance of everything under the path.</param>
    /// <param name="fileSystem">Resolves the path and writes machine-readable output when asked.</param>
    /// <param name="serializers">Report serialisers, used for <c>diff --format sarif</c>.</param>
    public ProvenanceCommandRunner(
        ProvenanceInspector inspector,
        IFileSystem fileSystem,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(serializers);

        _inspector = inspector;
        _fileSystem = fileSystem;
        _serializers = [.. serializers];
    }

    /// <summary>Reports where each asset under a directory came from.</summary>
    /// <param name="request">What to scan and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns><see cref="ExitCodes.Success"/>, or <see cref="ExitCodes.InvalidUsage"/> for a path that is not there.</returns>
    internal async Task<int> RunAsync(ProvenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_fileSystem.DirectoryExists(request.Path))
        {
            await Console.Error
                .WriteLineAsync($"'{request.Path}' is not a directory, so there is nothing to trace.")
                .ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        var report = await _inspector
            .InspectAsync(request.Path, includeLocalModifications: true, cancellationToken)
            .ConfigureAwait(false);

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(report)
            : ToText(report);

        await WriteAsync(text, request.OutputPath, cancellationToken).ConfigureAwait(false);

        return ExitCodes.Success;
    }

    /// <summary>Compares where two trees' assets came from.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> unless <c>--fail-on-drift</c> was given and something drifted. Like every
    /// other diff here it takes two paths rather than a revision range; <c>docs/ci.md</c> carries the
    /// <c>git worktree</c> recipe that does the same job today.
    /// </returns>
    internal async Task<int> DiffAsync(
        ProvenanceDiffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var path in new[] { request.BeforePath, request.AfterPath })
        {
            if (!_fileSystem.DirectoryExists(path))
            {
                await Console.Error
                    .WriteLineAsync($"'{path}' is not a directory, so there is nothing to compare.")
                    .ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }
        }

        var before = await _inspector
            .InspectAsync(request.BeforePath, includeLocalModifications: true, cancellationToken)
            .ConfigureAwait(false);

        var after = await _inspector
            .InspectAsync(request.AfterPath, includeLocalModifications: true, cancellationToken)
            .ConfigureAwait(false);

        var diff = ProvenanceDiffer.Compare(before, after);
        var findings = ProvenanceChangeDiagnostics.From(diff);

        var content = request.Format switch
        {
            _ when string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase) =>
                ToJson(diff, findings),
            _ when string.Equals(request.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase) =>
                ToSarif(diff, findings),
            _ => ToText(diff, findings),
        };

        await WriteAsync(content, request.OutputPath, cancellationToken).ConfigureAwait(false);

        return request.FailOnDrift && findings.Count > 0
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    private async Task WriteAsync(string content, string? outputPath, CancellationToken cancellationToken)
    {
        if (outputPath is not { Length: > 0 })
        {
            await Console.Out.WriteAsync(content).ConfigureAwait(false);
            return;
        }

        var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
        if (directory is { Length: > 0 })
        {
            _fileSystem.CreateDirectory(directory);
        }

        await _fileSystem.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
    }

    private static string ToText(ProvenanceReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Provenance");
        builder.AppendLine();
        builder.AppendLine($"Root:       {report.Root}");
        builder.AppendLine($"Repository: {Or(report.Repository)}");
        builder.AppendLine($"Commit:     {Or(report.Commit)}");

        builder.AppendLine();
        builder.AppendLine($"Assets ({report.Assets.Count}):");

        if (report.Assets.Count == 0)
        {
            builder.AppendLine("  (none found)");
        }

        foreach (var asset in report.Assets)
        {
            AppendAsset(builder, asset);
        }

        AppendDistribution(builder, report.Distribution);
        AppendDiagnostics(builder, report);

        builder.AppendLine();
        builder.AppendLine(
            "Every value above was read from a manifest, a marketplace file, the git checkout or the bytes on "
                + "disk. 'unknown' means nothing said, never a guess.");
        builder.AppendLine(
            "Recorded provenance is not a signature: it says what the source claimed, not that the claim was "
                + "verified.");

        return builder.ToString();
    }

    private static void AppendAsset(StringBuilder builder, AssetProvenance asset)
    {
        builder.AppendLine();
        builder.AppendLine($"  {asset.Name} [{asset.Kind}] {asset.Path}");
        builder.AppendLine($"      Status:       {Word(asset.Status)}");
        builder.AppendLine($"      Origin:       {Or(asset.Source.RepositoryUrl)}");
        builder.AppendLine($"      Version:      {Or(asset.Source.Version)}");
        builder.AppendLine($"      Commit:       {Or(asset.Source.CommitSha)}");
        builder.AppendLine($"      SHA-256:      {Or(asset.Source.Sha256)}");
        builder.AppendLine($"      Publisher:    {Or(asset.Source.Publisher)}");
        builder.AppendLine($"      Marketplace:  {Or(asset.Source.Marketplace)}");
        builder.AppendLine($"      Update mode:  {asset.Source.UpdateMode}");

        // Said out loud, because an inherited publisher must not read as one the asset declared itself.
        if (asset.DistributedBy is { Length: > 0 } plugin)
        {
            builder.AppendLine(
                $"      Distributed:  by {plugin} — publisher, marketplace and update mode are inherited");
        }

        if (asset.DeclaredUpstream is { Length: > 0 } upstream)
        {
            builder.AppendLine($"      Upstream:     {upstream} (declared by the asset, not verified)");
        }

        if (asset.LocallyModifiedFiles > 0)
        {
            builder.AppendLine($"      Modified:     {asset.LocallyModifiedFiles} uncommitted file(s)");
        }

        builder.AppendLine($"      Evidence:     {string.Join(", ", asset.Evidence)}");
    }

    private static void AppendDistribution(StringBuilder builder, DistributionSummary distribution)
    {
        builder.AppendLine();
        builder.AppendLine("Distribution");
        builder.AppendLine("------------");
        builder.AppendLine($"  Marketplaces          {distribution.Marketplaces}");
        builder.AppendLine($"  Auto-update enabled   {distribution.AutomaticUpdates}");
        builder.AppendLine($"  Floating assets       {distribution.FloatingAssets}");
        builder.AppendLine($"  Pinned assets         {distribution.PinnedAssets}");
        builder.AppendLine($"  Unknown provenance    {distribution.UnknownProvenance}");
    }

    private static void AppendDiagnostics(StringBuilder builder, ProvenanceReport report)
    {
        if (report.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Could not read:");

        foreach (var diagnostic in report.Diagnostics)
        {
            builder.AppendLine($"  ! {diagnostic.Code} {diagnostic.Message}");
        }
    }

    private static string ToJson(ProvenanceReport report)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["root"] = report.Root,
            ["repository"] = report.Repository,
            ["commit"] = report.Commit,
            ["assets"] = new JsonArray([.. report.Assets.Select(ToJson)]),
            ["distribution"] = ToJson(report.Distribution),
            ["diagnostics"] = new JsonArray(
                [.. report.Diagnostics.Select(diagnostic => (JsonNode)new JsonObject
                {
                    ["code"] = diagnostic.Code,
                    ["severity"] = diagnostic.Severity.ToString().ToLowerInvariant(),
                    ["message"] = diagnostic.Message,
                    ["filePath"] = diagnostic.FilePath,
                })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    /// <summary>
    /// Writes one asset. Unknown fields are written as <c>null</c> rather than omitted, so a consumer reads
    /// "SkillForge looked and found nothing" instead of "this report predates the field".
    /// </summary>
    private static JsonNode ToJson(AssetProvenance asset) => new JsonObject
    {
        ["name"] = asset.Name,
        ["kind"] = asset.Kind.ToString().ToLowerInvariant(),
        ["path"] = asset.Path,
        ["status"] = asset.Status.ToString().ToLowerInvariant(),
        ["marketplace"] = asset.Source.Marketplace,
        ["publisher"] = asset.Source.Publisher,
        ["repositoryUrl"] = asset.Source.RepositoryUrl,
        ["version"] = asset.Source.Version,
        ["commitSha"] = asset.Source.CommitSha,
        ["sha256"] = asset.Source.Sha256,
        ["updateMode"] = asset.Source.UpdateMode.ToString().ToLowerInvariant(),

        // The plugin the marketplace, publisher and update mode above were inherited from, or null when the asset
        // declared them itself. A consumer must be able to tell a derived value from an observed one.
        ["distributedBy"] = asset.DistributedBy,
        ["declaredUpstream"] = asset.DeclaredUpstream,
        ["locallyModifiedFiles"] = asset.LocallyModifiedFiles,
        ["evidence"] = new JsonArray([.. asset.Evidence.Select(value => (JsonNode)JsonValue.Create(value))]),
    };

    private static JsonObject ToJson(DistributionSummary distribution) => new()
    {
        ["marketplaces"] = distribution.Marketplaces,
        ["automaticUpdates"] = distribution.AutomaticUpdates,
        ["floatingAssets"] = distribution.FloatingAssets,
        ["pinnedAssets"] = distribution.PinnedAssets,
        ["unknownProvenance"] = distribution.UnknownProvenance,
    };

    private static string ToText(ProvenanceDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Provenance Diff");
        builder.AppendLine();
        builder.AppendLine($"Before: {diff.BeforePath}");
        builder.AppendLine($"After:  {diff.AfterPath}");
        builder.AppendLine();

        if (!diff.HasChanges)
        {
            builder.AppendLine("Both trees say the same thing about where everything came from.");
            return builder.ToString();
        }

        foreach (var change in diff.Changed)
        {
            builder.AppendLine($"  {change.Name} [{change.Kind}] {change.Path}");
            AppendChange(builder, "Publisher", change.Publisher);
            AppendChange(builder, "Marketplace", change.Marketplace);
            AppendChange(builder, "Repository", change.Repository);
            AppendChange(builder, "Version", change.Version);
            AppendChange(builder, "Commit", change.Commit);
            AppendChange(builder, "SHA-256", change.Fingerprint);
            AppendChange(builder, "Update mode", change.UpdateMode);
            AppendChange(builder, "Upstream", change.Upstream);
            builder.AppendLine();
        }

        AppendAssets(builder, "Arrived", diff.Added);
        AppendAssets(builder, "Gone", diff.Removed);

        if (findings.Count == 0)
        {
            builder.AppendLine("Nothing drifted: what changed says nothing about where these came from.");
            return builder.ToString();
        }

        builder.AppendLine("Drift:");
        foreach (var finding in findings)
        {
            builder.AppendLine($"  {Mark(finding.Severity)} {finding.Code} {finding.Message}");
        }

        return builder.ToString();
    }

    private static void AppendChange(StringBuilder builder, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine($"      {label,-12} {Or(change.Before)} -> {Or(change.After)}");
    }

    private static void AppendAssets(StringBuilder builder, string heading, IReadOnlyList<AssetProvenance> assets)
    {
        if (assets.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{heading}:");

        foreach (var asset in assets)
        {
            builder.AppendLine(
                $"  {asset.Name} [{asset.Kind}] {asset.Path} — {Word(asset.Status)}, "
                + $"publisher {Or(asset.Source.Publisher)}, update mode {asset.Source.UpdateMode}");
        }

        builder.AppendLine();
    }

    private static string Mark(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "x",
        DiagnosticSeverity.Warning => "!",
        _ => "i",
    };

    /// <summary>
    /// Reuses the shared SARIF serialiser rather than writing a second one, so a drift finding looks exactly like
    /// every other SkillForge finding in a code-scanning view.
    /// </summary>
    private string ToSarif(ProvenanceDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var report = new ValidationReport(
            string.Empty,
            diff.AfterPath,
            findings,
            ValidationSummary.FromDiagnostics(findings));

        return _serializers
            .Single(candidate =>
                string.Equals(candidate.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase))
            .Serialize(report);
    }

    private static string ToJson(ProvenanceDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["before"] = diff.BeforePath,
            ["after"] = diff.AfterPath,
            ["hasChanges"] = diff.HasChanges,
            ["drifted"] = findings.Count > 0,
            ["changed"] = new JsonArray([.. diff.Changed.Select(change => (JsonNode)new JsonObject
            {
                ["name"] = change.Name,
                ["kind"] = change.Kind.ToString().ToLowerInvariant(),
                ["path"] = change.Path,
                ["publisher"] = ToJson(change.Publisher),
                ["marketplace"] = ToJson(change.Marketplace),
                ["repositoryUrl"] = ToJson(change.Repository),
                ["version"] = ToJson(change.Version),
                ["commitSha"] = ToJson(change.Commit),
                ["sha256"] = ToJson(change.Fingerprint),
                ["updateMode"] = ToJson(change.UpdateMode),
                ["declaredUpstream"] = ToJson(change.Upstream),
            })]),
            ["added"] = new JsonArray([.. diff.Added.Select(ToJson)]),
            ["removed"] = new JsonArray([.. diff.Removed.Select(ToJson)]),
            ["findings"] = new JsonArray([.. findings.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject? ToJson(SurfaceValueChange? change) =>
        change is null ? null : new JsonObject { ["before"] = change.Before, ["after"] = change.After };

    private static string Or(string? value) => value is { Length: > 0 } ? value : "unknown";

    /// <summary>
    /// Writes a status as words rather than as the identifier. <c>VERIFIEDSOURCE</c> is a token; <c>VERIFIED
    /// SOURCE</c> is what somebody reads.
    /// </summary>
    private static string Word(ProvenanceStatus status) =>
        string.Concat(status.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : $"{character}")).ToUpperInvariant();
}
