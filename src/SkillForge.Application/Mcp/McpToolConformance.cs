using System.Globalization;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Checks the tools a server advertises against what the specification requires of them.
/// </summary>
/// <remarks>
/// **There is no "must be JSON Schema 2020-12" rule here, and that is the important part.** A secondhand summary said
/// 2026-07-28 requires it; the specification says <c>inputSchema</c> "defaults to 2020-12 if no <c>$schema</c> field is
/// present" and then shows an explicit <c>draft-07</c> schema as a valid example. Shipping that rule would have failed
/// conforming servers. The declared dialect is reported and never judged.
///
/// What the specification does state at MUST level, and what is therefore checked:
///
/// - <c>inputSchema</c> **MUST** be a valid JSON Schema object, not <c>null</c>. A full schema validator is not needed
///   to see that it is missing or is not an object at all, and that is where this stops — a structurally present schema
///   is taken at its word.
/// - An <c>x-mcp-header</c> annotation has strict constraints, and the obligation lands on the **client**: a Streamable
///   HTTP client **MUST** reject a tool definition that breaks them, excluding that tool from <c>tools/list</c>. A
///   server shipping one has a tool that simply will not appear, with nothing in its own logs to say so. That is worth
///   reporting even though nothing SkillForge does is affected by it.
///
/// Tool naming is SHOULD-level, so it is reported as the mildest kind of observation rather than treated as a defect.
/// </remarks>
public static class McpToolConformance
{
    /// <summary>Longest name the specification's guidance allows.</summary>
    private const int MaximumNameLength = 128;

    /// <summary>Types an <c>x-mcp-header</c> annotation may be applied to. <c>number</c> is excluded by name.</summary>
    private static readonly string[] PermittedHeaderTypes = ["integer", "string", "boolean"];

    /// <summary>
    /// Characters RFC 9110 allows in an HTTP field name, which is what an <c>x-mcp-header</c> value must be.
    /// </summary>
    private static readonly char[] TokenSpecials = ['!', '#', '$', '%', '&', '\'', '*', '+', '-', '.', '^', '_', '`', '|', '~'];

    /// <summary>
    /// Checks every tool a server reported.
    /// </summary>
    /// <param name="serverName">The server, for the messages.</param>
    /// <param name="sourcePath">The configuration the server was declared in, so a finding has somewhere to point.</param>
    /// <param name="tools">The tools, as read from <c>tools/list</c>.</param>
    /// <returns>What the tools break, as informational findings like the rest of the band.</returns>
    public static IReadOnlyList<Diagnostic> Check(
        string serverName,
        string sourcePath,
        IReadOnlyList<McpToolSummary> tools)
    {
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(tools);

        var findings = new List<Diagnostic>();

        foreach (var tool in tools)
        {
            if (!tool.HasObjectInputSchema)
            {
                findings.Add(Diagnostic.Info(
                    DiagnosticCodes.McpToolInputSchemaInvalid,
                    $"'{serverName}' tool '{tool.Name}' has no inputSchema object.",
                    sourcePath,
                    suggestion: "The specification requires inputSchema to be a valid JSON Schema object and not null. "
                        + "A tool with no parameters still needs one — '{ \"type\": \"object\", "
                        + "\"additionalProperties\": false }' is the recommended form."));
            }

            findings.AddRange(HeaderFindings(serverName, sourcePath, tool));
            findings.AddRange(NameFindings(serverName, sourcePath, tool));
        }

        findings.AddRange(DuplicateNameFindings(serverName, sourcePath, tools));

        return findings;
    }

    private static IEnumerable<Diagnostic> HeaderFindings(
        string serverName,
        string sourcePath,
        McpToolSummary tool)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var annotation in tool.HeaderAnnotations)
        {
            var problem = HeaderProblem(annotation, seen);

            if (problem is null)
            {
                continue;
            }

            yield return Diagnostic.Info(
                DiagnosticCodes.McpToolHeaderAnnotationInvalid,
                $"'{serverName}' tool '{tool.Name}' has an x-mcp-header on '{annotation.PropertyName}' that "
                    + $"{problem}.",
                sourcePath,
                suggestion: "A Streamable HTTP client must reject a tool definition whose x-mcp-header breaks these "
                    + "constraints, which means excluding this tool from its tool list entirely — the tool would "
                    + "silently not appear.");
        }
    }

    /// <summary>
    /// What is wrong with one annotation, in words that finish the sentence "an x-mcp-header that …", or
    /// <see langword="null"/> when nothing is.
    /// </summary>
    private static string? HeaderProblem(McpHeaderAnnotation annotation, HashSet<string> seen)
    {
        if (annotation.HeaderName.Length == 0)
        {
            return "is empty";
        }

        if (annotation.HeaderName.Any(character => character is '\r' or '\n'))
        {
            return "contains a carriage return or line feed";
        }

        if (!annotation.HeaderName.All(IsTokenCharacter))
        {
            return $"is not a valid HTTP field name ('{annotation.HeaderName}')";
        }

        // Case-insensitively unique within one inputSchema, because HTTP field names are case-insensitive.
        if (!seen.Add(annotation.HeaderName))
        {
            return $"repeats the header name '{annotation.HeaderName}' already used in this schema";
        }

        if (annotation.PropertyType is null)
        {
            return "is on a property that declares no type, where only integer, string and boolean are permitted";
        }

        return PermittedHeaderTypes.Contains(annotation.PropertyType, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"is on a '{annotation.PropertyType}' property, where only integer, string and boolean are permitted"
                + (annotation.PropertyType.Equals("number", StringComparison.OrdinalIgnoreCase)
                    ? " — number is named in the specification as not permitted"
                    : string.Empty);
    }

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || TokenSpecials.Contains(character);

    private static IEnumerable<Diagnostic> NameFindings(
        string serverName,
        string sourcePath,
        McpToolSummary tool)
    {
        var problem = tool.Name.Length switch
        {
            0 => "is empty",
            > MaximumNameLength => string.Create(
                CultureInfo.InvariantCulture,
                $"is {tool.Name.Length} characters, over the {MaximumNameLength} the guidance allows"),
            _ when !tool.Name.All(IsNameCharacter) => "uses characters outside letters, digits, '_', '-' and '.'",
            _ => null,
        };

        if (problem is not null)
        {
            yield return Diagnostic.Info(
                DiagnosticCodes.McpToolNameOutsideGuidance,
                $"'{serverName}' tool name '{tool.Name}' {problem}.",
                sourcePath,
                suggestion: "Naming is SHOULD-level guidance, not a hard requirement — but a client aggregating "
                    + "several servers has to disambiguate names, and an unusual one makes that harder.");
        }
    }

    private static bool IsNameCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.';

    /// <summary>
    /// Names are case-sensitive and should be unique within one server, so a genuine collision is a repeat of the exact
    /// same name rather than a difference of casing.
    /// </summary>
    private static IEnumerable<Diagnostic> DuplicateNameFindings(
        string serverName,
        string sourcePath,
        IReadOnlyList<McpToolSummary> tools) =>
        tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => Diagnostic.Info(
                DiagnosticCodes.McpToolNameOutsideGuidance,
                $"'{serverName}' declares {group.Count()} tools called '{group.Key}'.",
                sourcePath,
                suggestion: "Tool names should be unique within a server; a client cannot tell these apart."));
}
