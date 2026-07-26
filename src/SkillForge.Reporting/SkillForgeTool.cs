using System.Reflection;

namespace SkillForge.Reporting;

/// <summary>
/// Identifies the tool in machine-readable reports.
/// </summary>
/// <remarks>
/// The version is read from the assembly rather than hard-coded, so a report can never claim a version the
/// binary is not.
/// </remarks>
public static class SkillForgeTool
{
    /// <summary>Tool name as it appears in reports.</summary>
    public const string Name = "SkillForge";

    /// <summary>Schema version of SkillForge's own JSON report.</summary>
    public const string ReportSchemaVersion = "1.0";

    /// <summary>Informational URL for tooling that wants one.</summary>
    public const string InformationUri = "https://github.com/NetAnlatAkademi/skillforge";

    /// <summary>Gets the running tool version.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(SkillForgeTool).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (informational is { Length: > 0 })
        {
            // Strip the source-control suffix Roslyn appends: "0.1.0+abc123" reads as a version, not a build.
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            return plusIndex < 0 ? informational : informational[..plusIndex];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
