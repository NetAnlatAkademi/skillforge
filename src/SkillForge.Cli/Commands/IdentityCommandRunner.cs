using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Identity;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Identity;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge identity inspect</c> and <c>identity diff</c> do.
/// </summary>
/// <remarks>
/// The question these answer is not "is this server authorised" — it is **whose authority the agent is using**.
/// A person's OAuth session is bounded by what that person may do and disappears when they leave; a workload
/// identity is bounded by what the workload was granted, which is usually more, and does not.
///
/// **No credential value is ever read, stored or printed.** Everything reported comes from the names of
/// environment variables and headers, and from what a server's own <c>401</c> said. The output says so, every
/// time, because inference from a name is exactly as strong as the name and no stronger.
/// </remarks>
internal sealed class IdentityCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly McpIdentityInspector _inspector;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="inspector">Reads the identities a configuration uses.</param>
    /// <param name="fileSystem">Checks the files exist and writes machine-readable output when asked.</param>
    /// <param name="serializers">Report serialisers, used for <c>diff --format sarif</c>.</param>
    public IdentityCommandRunner(
        McpIdentityInspector inspector,
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

    /// <summary>Reports the identity each declared MCP server is reached with.</summary>
    /// <param name="request">What to read and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns><see cref="ExitCodes.Success"/>; a description has nothing to fail at.</returns>
    internal async Task<int> InspectAsync(
        IdentityInspectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_fileSystem.FileExists(request.Path))
        {
            await Console.Error
                .WriteLineAsync($"'{request.Path}' does not exist, so there is no configuration to read.")
                .ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        var report = await _inspector
            .InspectAsync(request.Path, request.Probe, cancellationToken)
            .ConfigureAwait(false);

        var content = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(report)
            : ToText(report);

        await WriteAsync(content, request.OutputPath, cancellationToken).ConfigureAwait(false);

        return ExitCodes.Success;
    }

    /// <summary>Compares the identities two configurations use.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> unless <c>--fail-on-drift</c> was given and something widened.
    /// </returns>
    internal async Task<int> DiffAsync(
        IdentityDiffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var path in new[] { request.BeforePath, request.AfterPath })
        {
            if (!_fileSystem.FileExists(path))
            {
                await Console.Error
                    .WriteLineAsync($"'{path}' does not exist, so there is nothing to compare.")
                    .ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }
        }

        var before = await _inspector
            .InspectAsync(request.BeforePath, request.Probe, cancellationToken).ConfigureAwait(false);
        var after = await _inspector
            .InspectAsync(request.AfterPath, request.Probe, cancellationToken).ConfigureAwait(false);

        var diff = IdentityDiffer.Compare(before, after);
        var findings = IdentityDiffer.Findings(diff);

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

    private static string ToText(McpIdentityReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge MCP Identity");
        builder.AppendLine();
        builder.AppendLine($"File: {report.Path}");
        builder.AppendLine();
        builder.AppendLine($"Servers ({report.Servers.Count}):");

        if (report.Servers.Count == 0)
        {
            builder.AppendLine("  (none declared)");
        }

        foreach (var server in report.Servers)
        {
            builder.AppendLine();
            builder.AppendLine($"  {server.ServerName}");
            builder.AppendLine($"      Identity:   {Words(server.Identity.Type)}");
            builder.AppendLine($"      Issuer:     {Or(server.Identity.Issuer)}");
            builder.AppendLine($"      Delegated:  {(server.Identity.IsDelegated ? "yes" : "no")}");
            builder.AppendLine(
                $"      Credential: {(server.Identity.IsLongLived ? "long-lived" : "short-lived or unknown")}");
            builder.AppendLine($"      Scopes:     {Join(server.Identity.Scopes)}");
            builder.AppendLine($"      Risk:       {server.Risk.ToString().ToUpperInvariant()}");
            builder.AppendLine($"      Reason:     {server.Reason}");
            builder.AppendLine($"      Evidence:   {Join(server.Evidence)}");
        }

        AppendDiagnostics(builder, report.Diagnostics);

        builder.AppendLine();
        builder.AppendLine(
            "Identity is inferred from the names of environment variables and headers, and from what a server's "
                + "own 401 asked for. No credential value is read.");
        builder.AppendLine(
            "Scopes and issuers come only from a probed server's challenge, so without --probe they are empty.");

        return builder.ToString();
    }

    private static void AppendDiagnostics(StringBuilder builder, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Observations:");

        foreach (var diagnostic in diagnostics)
        {
            builder.AppendLine($"  {Mark(diagnostic.Severity)} {diagnostic.Code} {diagnostic.Message}");
        }
    }

    private static string ToText(IdentityDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Identity Diff");
        builder.AppendLine();
        builder.AppendLine($"Before: {diff.BeforePath}");
        builder.AppendLine($"After:  {diff.AfterPath}");
        builder.AppendLine();

        if (!diff.HasChanges)
        {
            builder.AppendLine("Both configurations reach every server with the same identity.");
            return builder.ToString();
        }

        foreach (var change in diff.Changed)
        {
            builder.AppendLine($"  {change.ServerName}");
            AppendChange(builder, "Identity", change.Type);
            AppendChange(builder, "Issuer", change.Issuer);
            AppendChange(builder, "Delegated", change.Delegation);
            AppendChange(builder, "Credential", change.Lifetime);

            foreach (var scope in change.Scopes.Added)
            {
                builder.AppendLine($"      + scope     {scope}");
            }

            foreach (var scope in change.Scopes.Removed)
            {
                builder.AppendLine($"      - scope     {scope}");
            }

            builder.AppendLine();
        }

        AppendServers(builder, "Arrived", diff.Added);
        AppendServers(builder, "Gone", diff.Removed);

        if (findings.Count == 0)
        {
            builder.AppendLine("Nothing widened: no new scope, no new delegation, no longer-lived credential.");
            return builder.ToString();
        }

        builder.AppendLine("Widened:");
        foreach (var finding in findings)
        {
            builder.AppendLine($"  {Mark(finding.Severity)} {finding.Code} {finding.Message}");
        }

        return builder.ToString();
    }

    private static void AppendServers(
        StringBuilder builder,
        string heading,
        IReadOnlyList<McpServerIdentity> servers)
    {
        if (servers.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{heading}:");

        foreach (var server in servers)
        {
            builder.AppendLine($"  {server.ServerName} — {Words(server.Identity.Type)}");
        }

        builder.AppendLine();
    }

    private static void AppendChange(StringBuilder builder, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine($"      {label,-11} {Or(change.Before)} -> {Or(change.After)}");
    }

    private string ToSarif(IdentityDiff diff, IReadOnlyList<Diagnostic> findings)
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

    private static string ToJson(McpIdentityReport report)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["path"] = report.Path,
            ["servers"] = new JsonArray([.. report.Servers.Select(ToJson)]),
            ["diagnostics"] = new JsonArray([.. report.Diagnostics.Select(ToJson)]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    /// <summary>
    /// Writes one server's identity. The evidence is included because an inference from a name is worth exactly as
    /// much as the name it came from, and a consumer should be able to see it.
    /// </summary>
    private static JsonNode ToJson(McpServerIdentity server) => new JsonObject
    {
        ["server"] = server.ServerName,
        ["source"] = server.SourcePath,
        ["identity"] = server.Identity.Type.ToString().ToLowerInvariant(),
        ["issuer"] = server.Identity.Issuer,
        ["subject"] = server.Identity.Subject,
        ["delegated"] = server.Identity.IsDelegated,
        ["longLived"] = server.Identity.IsLongLived,
        ["scopes"] = new JsonArray([.. server.Identity.Scopes.Select(scope => (JsonNode)JsonValue.Create(scope))]),
        ["risk"] = server.Risk.ToString().ToLowerInvariant(),
        ["reason"] = server.Reason,
        ["evidence"] = new JsonArray([.. server.Evidence.Select(value => (JsonNode)JsonValue.Create(value))]),
    };

    private static string ToJson(IdentityDiff diff, IReadOnlyList<Diagnostic> findings)
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
            ["widened"] = findings.Count > 0,
            ["changed"] = new JsonArray([.. diff.Changed.Select(change => (JsonNode)new JsonObject
            {
                ["server"] = change.ServerName,
                ["identity"] = ToJson(change.Type),
                ["issuer"] = ToJson(change.Issuer),
                ["delegated"] = ToJson(change.Delegation),
                ["credential"] = ToJson(change.Lifetime),
                ["scopesAdded"] = new JsonArray(
                    [.. change.Scopes.Added.Select(scope => (JsonNode)JsonValue.Create(scope))]),
                ["scopesRemoved"] = new JsonArray(
                    [.. change.Scopes.Removed.Select(scope => (JsonNode)JsonValue.Create(scope))]),
            })]),
            ["added"] = new JsonArray([.. diff.Added.Select(ToJson)]),
            ["removed"] = new JsonArray([.. diff.Removed.Select(ToJson)]),
            ["findings"] = new JsonArray([.. findings.Select(ToJson)]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonNode ToJson(Diagnostic diagnostic) => new JsonObject
    {
        ["code"] = diagnostic.Code,
        ["severity"] = diagnostic.Severity.ToString().ToLowerInvariant(),
        ["message"] = diagnostic.Message,
        ["filePath"] = diagnostic.FilePath,
    };

    private static JsonObject? ToJson(SurfaceValueChange? change) =>
        change is null ? null : new JsonObject { ["before"] = change.Before, ["after"] = change.After };

    private static string Mark(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "x",
        DiagnosticSeverity.Warning => "!",
        _ => "i",
    };

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none observed)" : string.Join(", ", values);

    private static string Or(string? value) => value is { Length: > 0 } ? value : "unknown";

    private static string Words(AgentIdentityType type) =>
        string.Concat(type.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {char.ToLowerInvariant(character)}" : $"{character}"));
}
