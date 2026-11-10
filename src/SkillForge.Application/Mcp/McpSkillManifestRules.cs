using System.Globalization;
using System.Text.RegularExpressions;
using SkillForge.Domain.Mcp.Skills;

namespace SkillForge.Application.Mcp;

/// <summary>
/// What makes a skill entry unusable before a single byte is read.
/// </summary>
/// <remarks>
/// Each rule is a sentence of the extension's specification: a manifest that breaks one is one a host must not load, so
/// nothing is read and the reasons are reported together under <c>SF8306</c>. Reading files on a manifest that is not
/// what the specification says it is would be verifying against a baseline nobody defined.
/// </remarks>
public static partial class McpSkillManifestRules
{
    /// <summary>The digest algorithm the specification defines, and the only one SkillForge verifies.</summary>
    public const string DigestPrefix = "sha256:";

    /// <summary>The most files one skill may hold, from the specification's per-skill limits.</summary>
    public const int MaxResources = 512;

    /// <summary>The most bytes one skill may hold in total — 16 MiB — from the specification's per-skill limits.</summary>
    public const long MaxTotalBytes = 16L * 1024 * 1024;

    /// <summary>Every reason the entry cannot be verified, in a stable order. Empty when it can be.</summary>
    /// <param name="entry">The entry as the server described it.</param>
    /// <returns>The problems.</returns>
    public static IReadOnlyList<string> Problems(McpSkillEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var problems = new List<string>();

        if (entry.FrontmatterJson is null)
        {
            problems.Add("the entry carries no frontmatter object");
        }

        switch (entry.Manifest.Kind)
        {
            case McpSkillManifestKind.Invalid:
                problems.Add(entry.Manifest.Detail ?? "resources is neither an array nor \"dynamic\"");
                return problems;

            case McpSkillManifestKind.Dynamic:
                return problems;
        }

        var resources = entry.Manifest.Resources;

        if (resources.Count > MaxResources)
        {
            problems.Add($"the manifest names {Count(resources.Count)} files, over the specification's limit of {Count(MaxResources)}");
        }

        foreach (var resource in resources)
        {
            problems.AddRange(ResourceProblems(resource));
        }

        var total = DeclaredTotal(resources);
        if (total > MaxTotalBytes)
        {
            problems.Add($"the manifest declares {Count(total)} bytes, over the specification's limit of {Count(MaxTotalBytes)}");
        }

        foreach (var repeated in resources
            .GroupBy(resource => resource.Uri, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            problems.Add($"'{repeated}' is listed more than once");
        }

        if (!resources.Any(resource => string.Equals(resource.Uri, entry.Uri, StringComparison.Ordinal)))
        {
            problems.Add($"the manifest does not name the skill's own SKILL.md, '{entry.Uri}'");
        }

        return problems;
    }

    private static IEnumerable<string> ResourceProblems(McpSkillResource resource)
    {
        if (resource.Uri.Length == 0)
        {
            yield return "a manifest entry names no uri";
            yield break;
        }

        if (resource.Size is null)
        {
            yield return $"'{resource.Uri}' declares no size";
        }
        else if (resource.Size < 0)
        {
            yield return $"'{resource.Uri}' declares a negative size";
        }

        if (resource.Digest is not { } digest)
        {
            yield return $"'{resource.Uri}' declares no digest";
        }
        else if (!digest.StartsWith(DigestPrefix, StringComparison.Ordinal))
        {
            yield return $"'{resource.Uri}' declares a digest in an algorithm other than sha256";
        }
        else if (!Sha256Digest().IsMatch(digest))
        {
            yield return $"'{resource.Uri}' declares a sha256 digest that is not 64 lowercase hexadecimal characters";
        }
    }

    /// <summary>
    /// The declared sizes added up, stopping once past the limit. A server chooses these numbers, and two of them near
    /// <see cref="long.MaxValue"/> would overflow a plain sum and abort the run instead of being reported.
    /// </summary>
    private static long DeclaredTotal(IReadOnlyList<McpSkillResource> resources)
    {
        var total = 0L;

        foreach (var resource in resources)
        {
            total += Math.Clamp(resource.Size ?? 0, 0, MaxTotalBytes + 1);

            if (total > MaxTotalBytes)
            {
                return total;
            }
        }

        return total;
    }

    private static string Count(long value) => value.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Digest();
}
