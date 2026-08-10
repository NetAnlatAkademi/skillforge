using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Policy;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Policy;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge policy diff</c> does.
/// </summary>
/// <remarks>
/// A policy decides whether other code ships, and it is reviewed in the same pull requests as everything else —
/// where turning <c>api.company.com</c> into <c>*.company.com</c> is a one-character diff and a change of scope.
/// This reports the change of scope.
///
/// It reports **relaxations only**. A policy that got stricter is printed and warned about nowhere: a command that
/// flagged every edit would teach people to skip its output, and then the edit that mattered gets skipped too.
///
/// Like every other diff here it takes two paths rather than a revision range. Resolving <c>origin/main...HEAD</c>
/// means materialising a tree, which is a worktree or a <c>git archive</c> and a set of failure modes of its own;
/// <c>docs/ci.md</c> has the <c>git worktree</c> recipe that does the same job today.
/// </remarks>
internal sealed class PolicyDiffCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IPolicyReader _policyReader;
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="policyReader">Reads each policy file.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="renderer">Reports a policy that could not be read.</param>
    /// <param name="serializers">Report serialisers, used for <c>--format sarif</c>.</param>
    public PolicyDiffCommandRunner(
        IPolicyReader policyReader,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(policyReader);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(serializers);

        _policyReader = policyReader;
        _fileSystem = fileSystem;
        _renderer = renderer;
        _serializers = [.. serializers];
    }

    /// <summary>Compares two policy files.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(PolicyDiffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var before = await ReadAsync(request.BeforePath, request, cancellationToken).ConfigureAwait(false);
        if (before is null)
        {
            return ExitCodes.ValidationFailed;
        }

        var after = await ReadAsync(request.AfterPath, request, cancellationToken).ConfigureAwait(false);
        if (after is null)
        {
            return ExitCodes.ValidationFailed;
        }

        var diff = PolicyDiffer.Compare(request.BeforePath, before, request.AfterPath, after);
        var findings = PolicyChangeDiagnostics.From(diff);

        var content = request.Format switch
        {
            _ when string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase) =>
                ToJson(diff, findings),
            _ when string.Equals(request.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase) =>
                ToSarif(diff, findings),
            _ => ToText(diff, findings),
        };

        await WriteAsync(content, request.OutputPath, cancellationToken).ConfigureAwait(false);

        return request.FailOnWeakening && findings.Count > 0
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    /// <summary>
    /// Reads one side, or reports why it could not. A policy that will not parse fails the command rather than
    /// being compared as an empty one: an unreadable policy compared against a real one would report every rule in
    /// the other file as an addition or a removal, which is a story about a parse error told as a policy change.
    /// </summary>
    private async Task<PolicyDocument?> ReadAsync(
        string path,
        PolicyDiffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _policyReader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && result.Value is { } policy)
        {
            return policy;
        }

        _renderer.Render(
            ValidationReport.ForUnloadableSkill(path, result.Diagnostics),
            request.RenderOptions);

        return null;
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

    private string ToSarif(PolicyDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var report = new ValidationReport(
            string.Empty,
            diff.AfterPath,
            findings,
            ValidationSummary.FromDiagnostics(findings));

        var serializer = _serializers.Single(candidate =>
            string.Equals(candidate.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase));

        return serializer.Serialize(report);
    }

    private static string ToText(PolicyDiff diff, IReadOnlyList<Diagnostic> findings)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Policy Diff");
        builder.AppendLine();
        builder.AppendLine($"Before: {diff.BeforePath}");
        builder.AppendLine($"After:  {diff.AfterPath}");
        builder.AppendLine();

        if (!diff.HasChanges)
        {
            builder.AppendLine("The two policies decide the same things.");
            return builder.ToString();
        }

        AppendMcp(builder, diff.Mcp);
        AppendPermissions(builder, diff);
        AppendRequirements(builder, diff);

        AppendRules(builder, "Rules silenced", diff.Suppressions.Added);
        AppendRules(builder, "Rules no longer silenced", diff.Suppressions.Removed);

        if (findings.Count == 0)
        {
            builder.AppendLine("Nothing was relaxed.");
            return builder.ToString();
        }

        builder.AppendLine("Relaxed:");
        foreach (var finding in findings)
        {
            builder.AppendLine($"  {finding.Code} {finding.Message}");
        }

        return builder.ToString();
    }

    private static void AppendMcp(StringBuilder builder, McpPolicyDiff mcp)
    {
        if (!mcp.HasChanges)
        {
            return;
        }

        AppendValue(builder, "MCP default", mcp.Default);

        AppendRules(builder, "MCP newly permitted", [.. mcp.NewlyPermitted.Select(rule => rule.ToString())]);

        AppendRules(
            builder,
            "MCP widened",
            [.. mcp.Widenings.Select(widening => $"{widening.Generalises.Pattern} -> {widening.Rule.Pattern}")]);

        AppendRules(
            builder,
            "MCP no longer permitted",
            [
                .. mcp.AllowRemoved.Select(rule => $"{rule} (allow removed)"),
                .. mcp.DenyAdded.Select(rule => $"{rule} (deny added)"),
            ]);

        // Shown, never coded: no command enforces either rule today, and policy check says so as SF9009.
        AppendRules(builder, "MCP protocol versions accepted", mcp.AllowedProtocolVersions.Added);
        AppendRules(builder, "MCP protocol versions dropped", mcp.AllowedProtocolVersions.Removed);
        AppendValue(builder, "MCP deprecated capabilities", mcp.DenyDeprecatedCapabilities);
    }

    private static void AppendPermissions(StringBuilder builder, PolicyDiff diff)
    {
        AppendValue(builder, "Shell", diff.ShellAllowed);
        AppendValue(builder, "Filesystem write", diff.FilesystemWriteAllowed);
        AppendRules(builder, "Write paths added", diff.FilesystemWritePaths.Added);
        AppendRules(builder, "Write paths removed", diff.FilesystemWritePaths.Removed);
        AppendValue(builder, "Host allow-list", diff.AllowedDomainsDeclared);
        AppendRules(builder, "Hosts allowed", diff.AllowedDomains.Added);
        AppendRules(builder, "Hosts no longer allowed", diff.AllowedDomains.Removed);
    }

    private static void AppendRequirements(StringBuilder builder, PolicyDiff diff)
    {
        AppendValue(builder, "provenance.requireCommitSha", diff.RequireCommitSha);
        AppendValue(builder, "provenance.requirePackageHash", diff.RequirePackageHash);
        AppendValue(builder, "skills.requireLicense", diff.RequireLicense);
        AppendValue(builder, "skills.maxSkillFileLines", diff.MaxSkillFileLines);
    }

    private static void AppendValue(StringBuilder builder, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine($"{label}: {change.Before ?? "(none)"} -> {change.After ?? "(none)"}");
        builder.AppendLine();
    }

    private static void AppendRules(StringBuilder builder, string label, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{label}:");
        foreach (var entry in entries)
        {
            builder.AppendLine($"  {entry}");
        }

        builder.AppendLine();
    }

    private static string ToJson(PolicyDiff diff, IReadOnlyList<Diagnostic> findings)
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
            ["weakened"] = findings.Count > 0,
            ["permissions"] = new JsonObject
            {
                ["shellAllowed"] = ToJson(diff.ShellAllowed),
                ["filesystemWriteAllowed"] = ToJson(diff.FilesystemWriteAllowed),
                ["filesystemWritePaths"] = ToJson(diff.FilesystemWritePaths),
                ["allowedDomainsDeclared"] = ToJson(diff.AllowedDomainsDeclared),
                ["allowedDomains"] = ToJson(diff.AllowedDomains),
            },
            ["provenance"] = new JsonObject
            {
                ["requireCommitSha"] = ToJson(diff.RequireCommitSha),
                ["requirePackageHash"] = ToJson(diff.RequirePackageHash),
            },
            ["skills"] = new JsonObject
            {
                ["requireLicense"] = ToJson(diff.RequireLicense),
                ["maxSkillFileLines"] = ToJson(diff.MaxSkillFileLines),
            },
            ["suppressions"] = ToJson(diff.Suppressions),
            ["mcp"] = ToJson(diff.Mcp),
            ["findings"] = ToJson(findings),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject ToJson(McpPolicyDiff mcp) => new()
    {
        ["default"] = ToJson(mcp.Default),
        ["allowAdded"] = Rules(mcp.AllowAdded),
        ["allowRemoved"] = Rules(mcp.AllowRemoved),
        ["denyAdded"] = Rules(mcp.DenyAdded),
        ["denyRemoved"] = Rules(mcp.DenyRemoved),
        ["newlyPermitted"] = Rules(mcp.NewlyPermitted),
        ["widenings"] = new JsonArray(
        [
            .. mcp.Widenings.Select(widening => (JsonNode)new JsonObject
            {
                ["generalises"] = widening.Generalises.ToString(),
                ["rule"] = widening.Rule.ToString(),
            }),
        ]),
        ["allowedProtocolVersions"] = ToJson(mcp.AllowedProtocolVersions),
        ["denyDeprecatedCapabilities"] = ToJson(mcp.DenyDeprecatedCapabilities),
    };

    private static JsonArray Rules(IReadOnlyList<McpPolicyRule> rules) =>
        new(
        [
            .. rules.Select(rule => (JsonNode)new JsonObject
            {
                ["kind"] = rule.Kind.ToString(),
                ["pattern"] = rule.Pattern,
                ["arguments"] = new JsonArray([.. rule.Arguments.Select(a => (JsonNode)JsonValue.Create(a))]),
                ["hasWildcard"] = rule.HasWildcard,
            }),
        ]);

    private static JsonObject? ToJson(SurfaceValueChange? change) =>
        change is null ? null : new JsonObject { ["before"] = change.Before, ["after"] = change.After };

    private static JsonObject ToJson(SurfaceSetDiff diff) => new()
    {
        ["added"] = new JsonArray([.. diff.Added.Select(value => (JsonNode)JsonValue.Create(value))]),
        ["removed"] = new JsonArray([.. diff.Removed.Select(value => (JsonNode)JsonValue.Create(value))]),
    };

    private static JsonArray ToJson(IReadOnlyList<Diagnostic> findings) =>
        new(
        [
            .. findings.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            }),
        ]);
}
