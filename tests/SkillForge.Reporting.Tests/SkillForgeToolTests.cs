using System.Globalization;

namespace SkillForge.Reporting.Tests;

public sealed class SkillForgeToolTests
{
    [Fact]
    public void TheVersionFollowsTheDatedScheme()
    {
        // YY.DayOfYear.Build. No leading zeroes: SemVer forbids them in numeric identifiers, so a padded
        // "26.208.01" would not be a valid package version at all.
        SkillForgeTool.Version.Should().MatchRegex(@"^\d{2}\.([1-9]|[1-9]\d|[1-2]\d\d|3[0-5]\d|36[0-6])\.\d+$");
    }

    [Fact]
    public void TheVersionSaysWhenTheBuildWasMade()
    {
        var parts = SkillForgeTool.Version.Split('.');
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var dayOfYear = int.Parse(parts[1], CultureInfo.InvariantCulture);

        // Allowing yesterday and tomorrow keeps this from failing for a build that straddles UTC midnight or runs
        // on an agent in another timezone.
        var now = DateTime.UtcNow;
        var candidates = new[] { now.AddDays(-1), now, now.AddDays(1) }
            .Select(date => (Year: date.Year % 100, date.DayOfYear));

        candidates.Should().Contain((year, dayOfYear));
    }

    [Fact]
    public void TheVersionCarriesNoSourceControlSuffix()
    {
        // The assembly's informational version ends with "+<commit>"; a report should show the version, not the
        // build metadata.
        SkillForgeTool.Version.Should().NotContain("+");
    }

    [Fact]
    public void TheToolIdentifiesItself()
    {
        SkillForgeTool.Name.Should().Be("SkillForge");
        SkillForgeTool.ReportSchemaVersion.Should().Be("1.0");
        SkillForgeTool.InformationUri.Should().StartWith("https://");
    }
}
