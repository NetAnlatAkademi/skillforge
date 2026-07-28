using System.Text.RegularExpressions;

namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// The package README is what somebody reads on nuget.org before installing, and nuget.org renders it under rules
/// the repository's own Markdown does not follow.
/// </summary>
/// <remarks>
/// This exists because the package shipped the repository README for two releases, which put a broken logo and a
/// dozen dead <c>docs/...</c> links on the package page: nuget.org resolves neither relative links nor relative
/// images. Nothing failed, nothing warned, and the page simply looked neglected. A test is the only thing that
/// notices.
///
/// It lives here rather than in the CLI tests for the same reason the diagnostic-code documentation test does:
/// this project already has the machinery for reading a file out of the repository.
/// </remarks>
public sealed partial class PackageReadmeTests
{
    private static readonly string PackageReadmePath =
        Path.Combine(RepositoryPaths.RepositoryRoot, "src", "SkillForge.Cli", "PACKAGE.md");

    private static readonly string PackageReadme = File.ReadAllText(PackageReadmePath);

    [Fact]
    public void ThePackageReadmeExistsWhereTheProjectFileSaysItDoes()
    {
        // A missing PackageReadmeFile is a pack-time error, but only when somebody packs. This says it sooner.
        File.Exists(PackageReadmePath).Should().BeTrue();
    }

    [Fact]
    public void EveryLinkIsAbsoluteBecauseNuGetDoesNotResolveRelativeOnes()
    {
        var relative = MarkdownLink()
            .Matches(PackageReadme)
            .Select(match => match.Groups["target"].Value)
            .Where(target => !target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !target.StartsWith('#'))
            .ToArray();

        relative.Should().BeEmpty(
            "nuget.org renders a relative link as a dead one, so the package page has to carry full URLs");
    }

    [Fact]
    public void ItTellsTheReaderHowToInstallTheTool()
    {
        // The one question a package page is always asked.
        PackageReadme.Should().Contain("dotnet tool install --global SkillForge.Cli");
    }

    [Fact]
    public void ItNamesEveryCommandTheCliActuallyHas()
    {
        // A page that lists five of seven commands is worse than one that lists none, because a reader believes it.
        foreach (var command in new[] { "init", "validate", "inspect", "diff", "eval", "pack", "migrate inspect" })
        {
            PackageReadme.Should().Contain($"skillforge {command}", $"'{command}' is a shipped command");
        }
    }

    [Fact]
    public void ItRepeatsTheStanceThatSkillForgeDoesNotCallASkillSafe()
    {
        // ADR-006. The claim not to make is worth making on the page somebody reads first.
        PackageReadme.Should().Contain("safe");
    }

    /// <summary>Markdown inline links and images: the target inside the parentheses.</summary>
    [GeneratedRegex(@"!?\[[^\]]*\]\((?<target>[^)\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();
}
