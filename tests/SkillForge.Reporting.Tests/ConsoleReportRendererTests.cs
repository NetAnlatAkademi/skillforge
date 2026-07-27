using System.Text;
using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;
using Spectre.Console;

namespace SkillForge.Reporting.Tests;

public sealed class ConsoleReportRendererTests
{
    [Fact]
    public void ShowsTheSkillTheVerdictAndTheCounts()
    {
        var output = Render(Report(
            Diagnostic.Error(DiagnosticCodes.NameMissing, "The skill does not declare a name."),
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license is declared.")));

        output.Should().Contain("demo-skill");
        output.Should().Contain("SF0004");
        output.Should().Contain("SF1009");
        output.Should().Contain("INVALID");
        output.Should().Contain("Errors: 1");
        output.Should().Contain("Warnings: 1");
    }

    [Fact]
    public void SaysValidWhenThereIsNothingToReport()
    {
        Render(Report()).Should().Contain("VALID").And.NotContain("INVALID");
    }

    [Fact]
    public void DistinguishesWarningsFromACleanRun()
    {
        var output = Render(Report(Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.")));

        output.Should().Contain("VALID WITH WARNINGS");
    }

    [Fact]
    public void ShowsWhereEachFindingIs()
    {
        var output = Render(Report(
            Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "Missing file.", "SKILL.md", 12)));

        output.Should().Contain("SKILL.md:12");
    }

    [Fact]
    public void QuietModeDropsEverythingButErrorsAndTheVerdict()
    {
        var output = Render(
            Report(
                Diagnostic.Error(DiagnosticCodes.NameMissing, "No name."),
                Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.")),
            new ReportRenderOptions(Quiet: true));

        output.Should().Contain("SF0004");
        output.Should().NotContain("SF1009");
        output.Should().NotContain("Skill:");
        output.Should().Contain("INVALID");
    }

    [Fact]
    public void VerboseModeShowsTheSuggestion()
    {
        var report = Report(Diagnostic.Warning(
            DiagnosticCodes.LicenseMissing,
            "No license is declared.",
            "SKILL.md",
            1,
            "Add a 'license' field."));

        Render(report, new ReportRenderOptions(Verbose: true))
            .Should().Contain("Add a 'license' field.");
        Render(report).Should().NotContain("Add a 'license' field.");
    }

    [Fact]
    public void EachFindingCarriesATextMarkerSoOutputSurvivesWithoutColour()
    {
        var output = Render(
            Report(Diagnostic.Error(DiagnosticCodes.NameMissing, "No name.")),
            new ReportRenderOptions(NoColor: true));

        output.Should().Contain("x SF0004");
        output.Should().NotContain("[red]");
    }

    [Fact]
    public void SquareBracketsInAMessageDoNotBreakTheOutput()
    {
        // Spectre treats square brackets as markup; a message or path containing them must survive.
        var output = Render(Report(
            Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "Missing 'refs/[draft].md'.")));

        output.Should().Contain("refs/[draft].md");
    }

    [Fact]
    public void AnUnnamedSkillIsLabelledRatherThanBlank()
    {
        var report = ValidationReport.ForUnloadableSkill(
            "/skills/broken",
            [Diagnostic.Error(DiagnosticCodes.FrontmatterNotParsable, "Bad YAML.")]);

        Render(report).Should().Contain("(unnamed)");
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        var renderer = new ConsoleReportRenderer(CreateConsole(new StringBuilder()));

        var noReport = () => renderer.Render(null!, new ReportRenderOptions());
        var noOptions = () => renderer.Render(Report(), null!);
        var noConsole = () => new ConsoleReportRenderer(null!);

        noReport.Should().Throw<ArgumentNullException>();
        noOptions.Should().Throw<ArgumentNullException>();
        noConsole.Should().Throw<ArgumentNullException>();
    }

    private static string Render(ValidationReport report, ReportRenderOptions? options = null)
    {
        var buffer = new StringBuilder();
        new ConsoleReportRenderer(CreateConsole(buffer)).Render(report, options ?? new ReportRenderOptions());
        return buffer.ToString();
    }

    /// <summary>
    /// A console that writes plain text into a buffer, so assertions read what a user would see rather
    /// than a stream of escape codes.
    /// </summary>
    private static IAnsiConsole CreateConsole(StringBuilder buffer) =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(new StringWriter(buffer)),
        });

    [Fact]
    public void AFixIsShownWithoutAskingForVerbose()
    {
        // The whole point. A reader who has to pass --verbose to learn how to fix a one-line problem has been told
        // what is wrong and left to work out the schema themselves.
        var output = Render(Report(Diagnostic.Warning(
            DiagnosticCodes.LicenseMissing,
            "No license is declared.",
            fix: "add to the frontmatter:  license: MIT")));

        output.Should().Contain("fix");
        output.Should().Contain("license: MIT");
    }

    [Fact]
    public void AMultiLineFixKeepsItsShape()
    {
        var output = Render(Report(Diagnostic.Warning(
            DiagnosticCodes.ScriptWithoutDeclaredPermission,
            "The skill ships a script but declares no shell permission.",
            fix: """
                 create skillforge.yaml:
                   permissions:
                     shell:
                       allowed: [bash]
                 """)));

        output.Should().Contain("create skillforge.yaml:");
        output.Should().Contain("permissions:");
        output.Should().Contain("allowed: [bash]");
    }

    [Fact]
    public void AFindingWithNoFixShowsNoFixLine()
    {
        // Most findings have none. SF1007 points at a script that reaches further than usual, and only a human can
        // decide what should replace it — inventing a fix there would be worse than staying quiet.
        var output = Render(Report(Diagnostic.Warning(
            DiagnosticCodes.BroadShellPrivileges,
            "scripts/stop.sh uses recursive force delete.")));

        output.Should().NotContain("fix");
    }

    [Fact]
    public void TheFooterCountsTheFindingsThatHaveAFix()
    {
        var output = Render(Report(
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.", fix: "add: license: MIT"),
            Diagnostic.Warning(DiagnosticCodes.CompatibilityMissing, "No compatibility.", fix: "add: x"),
            Diagnostic.Warning(DiagnosticCodes.BroadShellPrivileges, "Recursive force delete.")));

        output.Should().Contain("Next:");
        output.Should().Contain("2 of these");
    }

    [Fact]
    public void TheFooterOffersTheSuppressCommandForTheRulesThatFireOnAlmostEverySkill()
    {
        var output = Render(Report(
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license."),
            Diagnostic.Warning(DiagnosticCodes.CompatibilityMissing, "No compatibility.")));

        // Measured at ~100% firing. Telling a reader the exact flag is more use than telling them the rule is noisy.
        output.Should().Contain("--suppress SF1009,SF1010");
    }

    [Fact]
    public void TheSuppressHintNamesOnlyTheCodesActuallyPresent()
    {
        var output = Render(Report(Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.")));

        output.Should().Contain("--suppress SF1009");
        output.Should().NotContain("SF1010");
    }

    [Fact]
    public void TheFooterAgreesWithItselfGrammatically()
    {
        // One code and two codes read differently, and "SF1010 fire on almost every skill" is the kind of sentence
        // that makes a reader trust the tool less for no good reason.
        var one = Render(Report(Diagnostic.Warning(DiagnosticCodes.CompatibilityMissing, "No compatibility.")));
        one.Should().Contain("SF1010 fires on").And.Contain("If it does not");

        var two = Render(Report(
            Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license."),
            Diagnostic.Warning(DiagnosticCodes.CompatibilityMissing, "No compatibility.")));
        two.Should().Contain("fire on").And.Contain("If they do not");
    }

    [Fact]
    public void OneFixableFindingReadsAsSingular()
    {
        var output = Render(Report(
            Diagnostic.Warning(DiagnosticCodes.BroadShellPrivileges, "Recursive force delete.", fix: "pin it")));

        output.Should().Contain("1 of these has a fix");
    }

    [Fact]
    public void ACleanRunHasNoFooter()
    {
        Render(Report()).Should().NotContain("Next:");
    }

    [Fact]
    public void QuietSuppressesTheFooter()
    {
        var output = Render(
            Report(Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "No license.", fix: "add: license: MIT")),
            new ReportRenderOptions { Quiet = true });

        output.Should().NotContain("Next:");
    }

    private static ValidationReport Report(params Diagnostic[] diagnostics) =>
        new("demo-skill", "/skills/demo", diagnostics, ValidationSummary.FromDiagnostics(diagnostics));
}
