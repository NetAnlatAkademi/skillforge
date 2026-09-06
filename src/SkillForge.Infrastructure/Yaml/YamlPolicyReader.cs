using SkillForge.Application.Abstractions;
using SkillForge.Application.Policy;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Policy;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SkillForge.Infrastructure.Yaml;

/// <summary>
/// Reads <c>.skillforge/policy.yaml</c> with YamlDotNet.
/// </summary>
/// <remarks>
/// Unlike <see cref="YamlSkillConfigurationReader"/>, a policy that cannot be parsed is a **failure** rather than a
/// warning with defaults. A skill's own configuration is advisory, so ignoring a broken one and saying so is the
/// kinder outcome; a policy is the organisation's decision, and a build that passes because the rules failed to
/// load is the worst thing this command could do.
///
/// A suppression with no reason is refused rather than applied, for the same reason: a policy that can silence a
/// rule without recording why has stopped being a record of decisions.
/// </remarks>
public sealed class YamlPolicyReader : IPolicyReader
{
    private const string PermissionsSection = "permissions";
    private const string RulesSection = "rules";
    private const string ShellSection = "shell";
    private const string FilesystemSection = "filesystem";
    private const string WriteSection = "write";
    private const string NetworkSection = "network";
    private const string ProvenanceSection = "provenance";
    private const string SkillsSection = "skills";
    private const string McpSection = "mcp";
    private const string SurfaceSection = "surface";
    private const string WarningToolCountField = "warningToolCount";
    private const string HighToolCountField = "highToolCount";
    private const string AllowField = "allow";
    private const string DenyField = "deny";
    private const string DefaultField = "default";
    private const string ServerUrlField = "serverUrl";
    private const string ServerCommandField = "serverCommand";
    private const string ServerNameField = "serverName";
    private const string CommandField = "command";
    private const string ArgumentsField = "args";
    private const string SuppressField = "suppress";
    private const string AllowedField = "allowed";
    private const string AllowedDomainsField = "allowedDomains";
    private const string AllowedProtocolVersionsField = "allowedProtocolVersions";
    private const string DenyDeprecatedCapabilitiesField = "denyDeprecatedCapabilities";
    private const string RequireCommitShaField = "requireCommitSha";
    private const string RequirePackageHashField = "requirePackageHash";
    private const string RequireLicenseField = "requireLicense";
    private const string MaxSkillFileLinesField = "maxSkillFileLines";
    private const string SchemaVersionField = "schemaVersion";
    private const string CodeField = "code";
    private const string SkillField = "skill";
    private const string ReasonField = "reason";

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">File system used to read the file.</param>
    public YamlPolicyReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PolicyDocument>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_fileSystem.FileExists(path))
        {
            return Failed(path, "it does not exist.");
        }

        string content;
        try
        {
            content = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(path, $"it could not be read: {exception.Message}");
        }

        YamlMappingNode? root;
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(content);
            stream.Load(reader);

            root = stream.Documents.Count == 0 ? null : stream.Documents[0].RootNode as YamlMappingNode;
        }
        catch (YamlException exception)
        {
            return Failed(path, FirstSentenceOf(exception.Message));
        }

        if (root is null)
        {
            // An empty policy file decides nothing, which is a legitimate thing to check in: it says the
            // organisation has a place for its rules and has not written any yet.
            return OperationResult<PolicyDocument>.Success(PolicyDocument.Empty);
        }

        // 'rules:' is where the documented schema puts everything, but a file that omits the wrapper says the
        // same thing, and rejecting it would be a parser being right at a user's expense.
        var rules = Section(root, RulesSection) ?? root;

        var permissions = Section(rules, PermissionsSection);
        var filesystemWrite = Section(Section(permissions, FilesystemSection), WriteSection);

        var (suppressions, suppressionFindings) = ReadSuppressions(root, path);
        var (mcp, mcpFindings) = ReadMcp(Section(rules, McpSection), path);

        var policy = new PolicyDocument(
            new PolicyPermissions(
                ReadNullableBoolean(Section(permissions, ShellSection), AllowedField),
                ReadFilesystemWriteAllowed(filesystemWrite),
                ReadFilesystemWritePaths(filesystemWrite),
                ReadNullableStrings(Section(permissions, NetworkSection), AllowedDomainsField)),
            new PolicyProvenance(
                ReadBoolean(Section(rules, ProvenanceSection), RequireCommitShaField),
                ReadBoolean(Section(rules, ProvenanceSection), RequirePackageHashField)),
            new PolicySkills(
                ReadBoolean(Section(rules, SkillsSection), RequireLicenseField),
                ReadInteger(Section(rules, SkillsSection), MaxSkillFileLinesField)),
            mcp,
            suppressions)
        {
            SchemaVersion = ReadInteger(root, SchemaVersionField) ?? 1,
        };

        return OperationResult<PolicyDocument>.Success(policy, [.. suppressionFindings, .. mcpFindings]);
    }

    /// <summary>
    /// Reads <c>allowed</c> under <c>filesystem.write</c>, which the documented schema writes two ways: a boolean,
    /// or a list of paths. A list means writing is permitted, and the paths are kept so the command can say it did
    /// not check them.
    /// </summary>
    private static bool? ReadFilesystemWriteAllowed(YamlMappingNode? write)
    {
        if (write is null || !write.Children.TryGetValue(new YamlScalarNode(AllowedField), out var node))
        {
            return null;
        }

        return node switch
        {
            YamlSequenceNode => true,
            YamlScalarNode { Value: { Length: > 0 } value } when bool.TryParse(value, out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// Paths only when a list was written. <c>allowed: false</c> is a decision, not a path called "false".
    /// </summary>
    private static string[] ReadFilesystemWritePaths(YamlMappingNode? write) =>
        write is not null
        && write.Children.TryGetValue(new YamlScalarNode(AllowedField), out var node)
        && node is YamlSequenceNode sequence
            ? Values(sequence)
            : [];

    /// <summary>
    /// Reads the <c>mcp</c> section.
    /// </summary>
    /// <remarks>
    /// An entry that cannot be interpreted is dropped and reported as <c>SF8103</c> rather than guessed at. Both
    /// halves matter: applying a rule nobody can read would enforce something nobody wrote, and dropping it quietly
    /// would leave an allow list shorter — or a deny list weaker — than its author believes.
    /// </remarks>
    private static (PolicyMcp? Mcp, IReadOnlyList<Diagnostic> Findings) ReadMcp(YamlMappingNode? mcp, string path)
    {
        if (mcp is null)
        {
            return (null, []);
        }

        var findings = new List<Diagnostic>();

        var (allow, allowFindings) = ReadRules(mcp, AllowField, path);
        var (deny, denyFindings) = ReadRules(mcp, DenyField, path);

        findings.AddRange(allowFindings);
        findings.AddRange(denyFindings);

        var section = new PolicyMcp(
            ReadStrings(mcp, AllowedProtocolVersionsField),
            ReadBoolean(mcp, DenyDeprecatedCapabilitiesField),
            ReadDefault(mcp, path, findings),
            allow,
            deny,
            ReadSurface(Section(mcp, SurfaceSection)));

        return (section, findings);
    }

    /// <summary>
    /// Reads <c>mcp.surface</c>. A section that sets one threshold and not the other keeps the default for the
    /// other, rather than being refused: half a decision is still a decision, and the defaults are documented.
    /// </summary>
    private static McpSurfaceThresholds? ReadSurface(YamlMappingNode? surface)
    {
        if (surface is null)
        {
            return null;
        }

        return new McpSurfaceThresholds(
            ReadInteger(surface, WarningToolCountField) ?? McpSurfaceThresholds.Default.WarningToolCount,
            ReadInteger(surface, HighToolCountField) ?? McpSurfaceThresholds.Default.HighToolCount);
    }

    /// <summary>
    /// Reads <c>default</c>. A value that is neither <c>allow</c> nor <c>deny</c> is refused rather than mapped to
    /// the nearest one: an unreadable default reads as no default, which is reported as <c>SF8104</c>.
    /// </summary>
    private static McpPolicyDefault ReadDefault(YamlMappingNode mcp, string path, List<Diagnostic> findings)
    {
        var value = ReadScalar(mcp, DefaultField);
        if (value is not { Length: > 0 })
        {
            return McpPolicyDefault.NotDeclared;
        }

        if (string.Equals(value, "deny", StringComparison.OrdinalIgnoreCase))
        {
            return McpPolicyDefault.Deny;
        }

        if (string.Equals(value, "allow", StringComparison.OrdinalIgnoreCase))
        {
            return McpPolicyDefault.Allow;
        }

        findings.Add(NotInterpretable(
            path,
            Line(mcp),
            $"mcp.default is '{value}', which is neither 'allow' nor 'deny'"));

        return McpPolicyDefault.NotDeclared;
    }

    /// <summary>Reads one rule list, keeping the order the file wrote.</summary>
    private static (IReadOnlyList<McpPolicyRule> Rules, IReadOnlyList<Diagnostic> Findings) ReadRules(
        YamlMappingNode mcp,
        string field,
        string path)
    {
        if (!mcp.Children.TryGetValue(new YamlScalarNode(field), out var node))
        {
            return ([], []);
        }

        if (node is not YamlSequenceNode entries)
        {
            return ([], [NotInterpretable(path, Line(node), $"mcp.{field} is not a list of rules")]);
        }

        var rules = new List<McpPolicyRule>();
        var findings = new List<Diagnostic>();

        foreach (var entry in entries)
        {
            if (ReadRule(entry, out var rule, out var reason))
            {
                rules.Add(rule!);
                continue;
            }

            findings.Add(NotInterpretable(path, Line(entry), $"an entry under mcp.{field} {reason}"));
        }

        return (rules, findings);
    }

    /// <summary>
    /// Reads one rule. <c>serverCommand</c> is accepted both as a bare command and as a mapping with arguments,
    /// because both are how people write it and rejecting the short form would be a parser being right at a user's
    /// expense.
    /// </summary>
    private static bool ReadRule(YamlNode entry, out McpPolicyRule? rule, out string reason)
    {
        rule = null;
        reason = string.Empty;

        if (entry is not YamlMappingNode mapping)
        {
            reason = "is not a mapping; write 'serverUrl:', 'serverCommand:' or 'serverName:'";
            return false;
        }

        if (ReadScalar(mapping, ServerUrlField) is { Length: > 0 } url)
        {
            rule = McpPolicyRule.Url(url);
            return true;
        }

        if (ReadScalar(mapping, ServerNameField) is { Length: > 0 } name)
        {
            rule = McpPolicyRule.Name(name);
            return true;
        }

        if (!mapping.Children.TryGetValue(new YamlScalarNode(ServerCommandField), out var command))
        {
            reason = "names none of 'serverUrl', 'serverCommand' or 'serverName'";
            return false;
        }

        switch (command)
        {
            case YamlScalarNode { Value: { Length: > 0 } single }:
                rule = McpPolicyRule.Command(single.Trim());
                return true;

            case YamlMappingNode details when ReadScalar(details, CommandField) is { Length: > 0 } executable:
                rule = new McpPolicyRule(
                    McpPolicyRuleKind.ServerCommand,
                    executable,
                    ReadStrings(details, ArgumentsField));

                return true;

            default:
                reason = "has a 'serverCommand' with no command in it";
                return false;
        }
    }

    private static Diagnostic NotInterpretable(string path, int? line, string reason) =>
        Diagnostic.Error(
            DiagnosticCodes.McpPolicyNotParsable,
            $"The MCP policy was not fully applied: {reason}.",
            path,
            line,
            suggestion: "Fix the entry. It was skipped, so it is protecting nothing as it stands.");

    private static int? Line(YamlNode node) => node.Start.Line > 0 ? (int)node.Start.Line : null;

    /// <summary>
    /// Reads the suppression list. An entry with no reason is dropped and reported: applying it would let a policy
    /// silence a rule without recording why, and dropping it silently would leave the author believing it worked.
    /// </summary>
    private static (IReadOnlyList<PolicySuppression> Suppressions, IReadOnlyList<Diagnostic> Findings)
        ReadSuppressions(YamlMappingNode root, string path)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(SuppressField), out var node)
            || node is not YamlSequenceNode entries)
        {
            return ([], []);
        }

        var suppressions = new List<PolicySuppression>();
        var findings = new List<Diagnostic>();

        foreach (var entry in entries.OfType<YamlMappingNode>())
        {
            var code = ReadScalar(entry, CodeField);
            if (code is not { Length: > 0 })
            {
                continue;
            }

            var reason = ReadScalar(entry, ReasonField);
            if (reason is not { Length: > 0 })
            {
                findings.Add(Diagnostic.Error(
                    DiagnosticCodes.PolicySuppressionWithoutReason,
                    $"The suppression of {code} gives no reason, so it was not applied.",
                    path,
                    suggestion: "Add a 'reason' naming the decision or the ticket that made it."));

                continue;
            }

            suppressions.Add(new PolicySuppression(code, ReadScalar(entry, SkillField), reason));
        }

        return (suppressions, findings);
    }

    private static OperationResult<PolicyDocument> Failed(string path, string reason) =>
        OperationResult<PolicyDocument>.Failure(Diagnostic.Error(
            DiagnosticCodes.PolicyNotParsable,
            $"The policy at {path} was not applied because {reason}",
            path,
            suggestion: "Fix the file or point --policy at another one. No policy was checked."));

    private static YamlMappingNode? Section(YamlMappingNode? parent, string name) =>
        parent is not null
        && parent.Children.TryGetValue(new YamlScalarNode(name), out var node)
        && node is YamlMappingNode section
            ? section
            : null;

    private static string? ReadScalar(YamlMappingNode section, string field) =>
        section.Children.TryGetValue(new YamlScalarNode(field), out var node)
        && node is YamlScalarNode { Value: { Length: > 0 } value }
            ? value.Trim()
            : null;

    private static bool? ReadNullableBoolean(YamlMappingNode? section, string field)
    {
        var value = section is null ? null : ReadScalar(section, field);

        return value is not null && bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool ReadBoolean(YamlMappingNode? section, string field) =>
        ReadNullableBoolean(section, field) ?? false;

    private static int? ReadInteger(YamlMappingNode? section, string field)
    {
        var value = section is null ? null : ReadScalar(section, field);

        return value is not null
            && int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    /// <summary>
    /// Reads a list that may be absent. Absent means the policy is silent; an empty list is a decision that
    /// nothing is allowed, and the two must not collapse into each other.
    /// </summary>
    private static string[]? ReadNullableStrings(YamlMappingNode? section, string field)
    {
        if (section is null || !section.Children.TryGetValue(new YamlScalarNode(field), out var node))
        {
            return null;
        }

        return node switch
        {
            YamlSequenceNode sequence => Values(sequence),
            YamlScalarNode { Value: { Length: > 0 } single } => [single.Trim()],
            _ => null,
        };
    }

    private static string[] ReadStrings(YamlMappingNode? section, string field) =>
        ReadNullableStrings(section, field) ?? [];

    private static string[] Values(YamlSequenceNode sequence) =>
        [
            .. sequence.Children
                .OfType<YamlScalarNode>()
                .Select(static item => item.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim()),
        ];

    private static string FirstSentenceOf(string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.EndsWith('.') ? firstLine : firstLine + ".";
    }
}
