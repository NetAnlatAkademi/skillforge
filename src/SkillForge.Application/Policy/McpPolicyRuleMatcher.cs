using System.Text;
using System.Text.RegularExpressions;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Policy;

/// <summary>
/// Decides whether an MCP policy rule names a given server, and whether one rule covers another.
/// </summary>
/// <remarks>
/// Everything a policy decision rests on lives here, so it can be argued with in one place.
///
/// **URLs are canonicalised before they are compared.** <c>HTTPS://MCP.Company.com:443/github/</c> and
/// <c>https://mcp.company.com/github</c> are the same endpoint, and a policy that blocked one while permitting the
/// other would be trivially evaded by whoever wrote the configuration.
///
/// **A rule only matches the transport it is about.** A <c>serverUrl</c> rule never matches a local command and a
/// <c>serverCommand</c> rule never matches a URL, so a policy cannot accidentally permit a process launch by
/// naming a web address.
///
/// **Comparison is case-insensitive throughout**, including URL paths, which are case-sensitive in the standard.
/// That is deliberate and it cuts one way: it makes an allow rule match slightly more than it strictly should. The
/// alternative makes a **deny** rule match less than it should, and a deny that misses is worse than an allow that
/// is generous — this is the same reasoning as denying before allowing.
/// </remarks>
public static class McpPolicyRuleMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Determines whether a rule names a declared server.</summary>
    /// <param name="rule">The policy rule.</param>
    /// <param name="server">The declaration read from a configuration file.</param>
    /// <returns><see langword="true"/> when the rule is about this server.</returns>
    public static bool Matches(McpPolicyRule rule, McpServerDeclaration server)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(server);

        return rule.Kind switch
        {
            McpPolicyRuleKind.ServerName => GlobMatches(rule.Pattern, server.Name),

            McpPolicyRuleKind.ServerUrl =>
                server.Transport == McpTransport.Http
                && server.Command is { Length: > 0 } url
                && GlobMatches(CanonicaliseUrl(rule.Pattern), CanonicaliseUrl(url)),

            _ =>
                server.Transport == McpTransport.Stdio
                && server.Command is { Length: > 0 } command
                && GlobMatches(rule.Pattern, command)
                && rule.Arguments.All(required =>
                    server.Arguments.Any(declared => GlobMatches(required, declared))),
        };
    }

    /// <summary>
    /// Determines whether one rule covers everything another covers, and more.
    /// </summary>
    /// <param name="broader">The candidate wider rule.</param>
    /// <param name="narrower">The rule it may subsume.</param>
    /// <returns><see langword="true"/> when <paramref name="broader"/> is a strict generalisation.</returns>
    /// <remarks>
    /// Answered by matching one pattern against the other as text, which is exact for the case that matters:
    /// <c>*.company.com</c> matches the literal <c>api.company.com</c>, and a rule with no wildcard can only match
    /// a pattern identical to itself. It is not a proof of subsumption for two patterns that both carry wildcards,
    /// and it does not claim to be — a widening it misses is reported as a new permission instead, which is the
    /// safe direction to be wrong in.
    /// </remarks>
    public static bool Generalises(McpPolicyRule broader, McpPolicyRule narrower)
    {
        ArgumentNullException.ThrowIfNull(broader);
        ArgumentNullException.ThrowIfNull(narrower);

        if (broader.Kind != narrower.Kind || broader == narrower)
        {
            return false;
        }

        var patternsMatch = broader.Kind == McpPolicyRuleKind.ServerUrl
            ? GlobMatches(CanonicaliseUrl(broader.Pattern), CanonicaliseUrl(narrower.Pattern))
            : GlobMatches(broader.Pattern, narrower.Pattern);

        return patternsMatch
            && broader.Arguments.All(required =>
                narrower.Arguments.Any(declared => GlobMatches(required, declared)));
    }

    /// <summary>
    /// Reduces a URL to the form two equivalent URLs share: lower-cased, without a default port, without a
    /// trailing slash.
    /// </summary>
    /// <param name="url">The URL or URL pattern, which may contain <c>*</c>.</param>
    /// <returns>The canonical form.</returns>
    /// <remarks>
    /// Done by hand rather than through <see cref="Uri"/>, because a pattern such as <c>http://*</c> is not a URI
    /// and parsing it would either throw or quietly rewrite it into something that matches differently.
    /// </remarks>
    public static string CanonicaliseUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var value = url.Trim();
        if (value.Length == 0)
        {
            return value;
        }

        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return TrimTrailingSlash(value.ToLowerInvariant());
        }

        var scheme = value[..schemeEnd].ToLowerInvariant();
        var rest = value[(schemeEnd + 3)..];

        var pathStart = rest.IndexOf('/');
        var authority = (pathStart < 0 ? rest : rest[..pathStart]).ToLowerInvariant();
        var path = pathStart < 0 ? string.Empty : rest[pathStart..];

        authority = DropDefaultPort(scheme, authority);

        return TrimTrailingSlash($"{scheme}://{authority}{path}");
    }

    /// <summary>
    /// Matches a <c>*</c> pattern against a value, ordinally and without regard to case.
    /// </summary>
    /// <param name="pattern">The pattern. Every character except <c>*</c> is literal.</param>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when the pattern covers the value.</returns>
    /// <remarks>
    /// Translated to a regular expression with everything but <c>*</c> escaped, so no character in a URL or a
    /// Windows path can be read as syntax. The timeout is insurance rather than a known risk: a pattern of escaped
    /// literals and <c>.*</c> cannot backtrack catastrophically, but a policy file is an input and an input that
    /// can hang a CI step is worth one line to prevent.
    /// </remarks>
    public static bool GlobMatches(string pattern, string value)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(value);

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(pattern.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        var segments = pattern.Trim().Split('*');
        var expression = new StringBuilder("^");

        for (var index = 0; index < segments.Length; index++)
        {
            if (index > 0)
            {
                expression.Append(".*");
            }

            expression.Append(Regex.Escape(segments[index]));
        }

        expression.Append('$');

        return Regex.IsMatch(
            value.Trim(),
            expression.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            MatchTimeout);
    }

    private static string DropDefaultPort(string scheme, string authority) => scheme switch
    {
        "http" when authority.EndsWith(":80", StringComparison.Ordinal) => authority[..^3],
        "https" when authority.EndsWith(":443", StringComparison.Ordinal) => authority[..^4],
        _ => authority,
    };

    private static string TrimTrailingSlash(string value) =>
        value.Length > 1 && value.EndsWith('/') ? value[..^1] : value;
}
