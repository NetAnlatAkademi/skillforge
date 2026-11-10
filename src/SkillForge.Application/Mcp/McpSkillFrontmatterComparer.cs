using SkillForge.Domain.Skills;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Compares the frontmatter a server described with the one in the <c>SKILL.md</c> it served.
/// </summary>
/// <remarks>
/// Both sides arrive as <see cref="SkillFrontmatter"/> from the same parser, so "comparable" means exactly the fields
/// SkillForge models: <c>name</c>, <c>description</c>, <c>license</c>, <c>compatibility</c>, <c>allowed-tools</c> and
/// the scalar entries of <c>metadata</c>. A field outside that set is not compared, and the documentation says so —
/// silence about a field nobody parsed is not agreement about it.
/// </remarks>
public static class McpSkillFrontmatterComparer
{
    /// <summary>Lists the fields that disagree.</summary>
    /// <param name="described">The <c>frontmatter</c> from <c>skills/get</c>.</param>
    /// <param name="served">The frontmatter parsed out of the served <c>SKILL.md</c>.</param>
    /// <returns>One line per field, <c>field: described · in SKILL.md</c>, in a fixed order. Empty when they agree.</returns>
    public static IReadOnlyList<string> Differences(SkillFrontmatter described, SkillFrontmatter served)
    {
        ArgumentNullException.ThrowIfNull(described);
        ArgumentNullException.ThrowIfNull(served);

        var differences = new List<string>();

        Scalar(differences, "name", described.Name, served.Name);
        Scalar(differences, "description", described.Description, served.Description);
        Scalar(differences, "license", described.License, served.License);
        Sequence(differences, "compatibility", described.Compatibility, served.Compatibility);
        Sequence(differences, "allowed-tools", described.AllowedTools, served.AllowedTools);

        foreach (var key in described.Metadata.Keys
            .Union(served.Metadata.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            Scalar(
                differences,
                $"metadata.{key}",
                described.Metadata.GetValueOrDefault(key),
                served.Metadata.GetValueOrDefault(key));
        }

        return differences;
    }

    private static void Scalar(List<string> differences, string field, string? described, string? served)
    {
        if (!string.Equals(described, served, StringComparison.Ordinal))
        {
            differences.Add($"{field}: {Show(described)} · {Show(served)}");
        }
    }

    private static void Sequence(
        List<string> differences,
        string field,
        IReadOnlyList<string> described,
        IReadOnlyList<string> served)
    {
        if (!described.SequenceEqual(served, StringComparer.Ordinal))
        {
            differences.Add($"{field}: [{string.Join(", ", described)}] · [{string.Join(", ", served)}]");
        }
    }

    private static string Show(string? value) => value is null ? "(absent)" : $"'{value}'";
}
