namespace SkillForge.Domain.Policy;

/// <summary>
/// One entry in an MCP policy's allow or deny list.
/// </summary>
/// <remarks>
/// A rule is data, not a decision: whether it appeared under <c>allow</c> or <c>deny</c> is the list's business, and
/// what it matches is <c>McpPolicyRuleMatcher</c>'s. Keeping the rule itself inert is what lets the same type be
/// compared between two policy files by <c>policy diff</c> without either side evaluating anything.
/// </remarks>
/// <param name="Kind">How the rule identifies a server.</param>
/// <param name="Pattern">
/// The URL, command or name it matches, as written. <c>*</c> matches any run of characters; everything else is
/// literal. The pattern is kept verbatim so a report can quote the line somebody wrote.
/// </param>
/// <param name="Arguments">
/// Arguments a <see cref="McpPolicyRuleKind.ServerCommand"/> rule also requires, as written. Empty for every other
/// kind, and for a command rule that cares only about the command.
/// </param>
public sealed record McpPolicyRule(
    McpPolicyRuleKind Kind,
    string Pattern,
    IReadOnlyList<string> Arguments)
{
    /// <summary>Creates a rule matching a remote server by URL.</summary>
    /// <param name="pattern">The URL pattern.</param>
    /// <returns>The rule.</returns>
    public static McpPolicyRule Url(string pattern) => new(McpPolicyRuleKind.ServerUrl, pattern, []);

    /// <summary>Creates a rule matching a local server by the command it launches.</summary>
    /// <param name="command">The command pattern.</param>
    /// <param name="arguments">Arguments the declaration must also carry.</param>
    /// <returns>The rule.</returns>
    public static McpPolicyRule Command(string command, params string[] arguments) =>
        new(McpPolicyRuleKind.ServerCommand, command, arguments);

    /// <summary>Creates a rule matching a server by the name its configuration gives it.</summary>
    /// <param name="name">The name pattern.</param>
    /// <returns>The rule.</returns>
    public static McpPolicyRule Name(string name) => new(McpPolicyRuleKind.ServerName, name, []);

    /// <summary>Gets a value indicating whether the rule matches more than one literal value.</summary>
    public bool HasWildcard =>
        Pattern.Contains('*', StringComparison.Ordinal)
        || Arguments.Any(argument => argument.Contains('*', StringComparison.Ordinal));

    /// <summary>Describes the rule the way it would be written in the policy file.</summary>
    /// <returns>A single line naming the kind and what it matches.</returns>
    public override string ToString() => Kind switch
    {
        McpPolicyRuleKind.ServerUrl => $"serverUrl: {Pattern}",
        McpPolicyRuleKind.ServerName => $"serverName: {Pattern}",
        _ when Arguments.Count > 0 => $"serverCommand: {Pattern} {string.Join(' ', Arguments)}",
        _ => $"serverCommand: {Pattern}",
    };

    /// <summary>Determines whether another rule decides the same thing.</summary>
    /// <param name="other">The rule to compare with.</param>
    /// <returns><see langword="true"/> when both would match exactly the same servers.</returns>
    /// <remarks>
    /// Written by hand because the compiler's version compares <see cref="Arguments"/> by reference, and
    /// <c>policy diff</c> is entirely a matter of asking whether a rule read from one file is present in another.
    /// Case is ignored here for the same reason the matcher ignores it: two rules differing only in case match the
    /// same servers, so calling them different rules would report an edit that changes nothing.
    /// </remarks>
    public bool Equals(McpPolicyRule? other) =>
        other is not null
        && Kind == other.Kind
        && string.Equals(Pattern, other.Pattern, StringComparison.OrdinalIgnoreCase)
        && Arguments.SequenceEqual(other.Arguments, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Pattern, StringComparer.OrdinalIgnoreCase);

        foreach (var argument in Arguments)
        {
            hash.Add(argument, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode();
    }
}
