using SkillForge.Application.Tests.Fakes;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class NetworkDeclarationRuleTests
{
    private readonly NetworkDeclarationRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.ExternalUrlPresent);

    [Fact]
    public async Task SaysNothingWhenTheSkillDeclaresNothingAboutTheNetwork()
    {
        // The measured reason this rule is not "a URL is present": that fires on 60 of 203 real skills and says
        // nothing. Without a declaration there is no contradiction to report.
        var skill = new SkillBuilder().WithBody("See [docs](https://learn.microsoft.com/x).").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task SaysNothingWhenTheNetworkIsAllowed()
    {
        var skill = Skill("See https://example.com/x", networkAllowed: true);

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsWhenAUrlContradictsADeclarationOfNoNetwork()
    {
        var skill = Skill("See [telemetry](https://api.example.com/v1).", networkAllowed: false);

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("api.example.com");
        diagnostic.Message.Should().Contain("network.allowed: false");
    }

    [Fact]
    public async Task ReportsEachHostOnceEvenWhenItAppearsRepeatedly()
    {
        var skill = Skill(
            "https://api.example.com/a\nhttps://api.example.com/b\nhttps://other.example.org/c",
            networkAllowed: false);

        (await _rule.Run(skill)).Should().HaveCount(2);
    }

    [Fact]
    public async Task SaysNothingWhenThereIsNoUrlToContradictTheDeclaration()
    {
        var skill = Skill("No links here at all.", networkAllowed: false);

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    private static Domain.Skills.SkillDefinition Skill(string body, bool networkAllowed) =>
        new SkillBuilder().WithBody(body).Build() with
        {
            Configuration = SkillConfiguration.Default with { Exists = true, NetworkAllowed = networkAllowed },
        };
}

public sealed class ScriptPermissionRuleTests
{
    private readonly ScriptPermissionRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() =>
        _rule.Code.Should().Be(DiagnosticCodes.ScriptWithoutDeclaredPermission);

    [Fact]
    public async Task SaysNothingWhenTheSkillShipsNoScript()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "references/notes.md").Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsWhenAScriptShipsWithNoDeclaredShellPermission()
    {
        // Measured on 203 real skills, 7 ship a script — so this speaks up about a few percent, which is what a
        // warning should feel like.
        var skill = new SkillBuilder().WithResources("SKILL.md", "scripts/run.ps1").Build();

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("scripts/run.ps1");

        // SKILL.md, not skillforge.yaml, because this skill has no skillforge.yaml. A finding must not point at a
        // file that does not exist: a reader opens it and finds nothing, and a SARIF consumer annotates a path
        // that is not in the repository at all.
        diagnostic.FilePath.Should().Be("SKILL.md");
        diagnostic.Line.Should().Be(1);
    }

    [Fact]
    public async Task PointsAtTheConfigurationFileWhenThereIsOne()
    {
        // When the file exists, it is both the place to fix and a real location, so it wins.
        var skill = new SkillBuilder().WithResources("SKILL.md", "scripts/run.ps1").Build() with
        {
            Configuration = SkillConfiguration.Default with { Exists = true },
        };

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.FilePath.Should().Be("skillforge.yaml");
    }

    [Fact]
    public async Task NamesEveryScriptInOneFinding()
    {
        var skill = new SkillBuilder()
            .WithResources("SKILL.md", "scripts/a.sh", "scripts/b.py")
            .Build();

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Message.Should().Contain("2 scripts");
        diagnostic.Message.Should().Contain("scripts/a.sh");
        diagnostic.Message.Should().Contain("scripts/b.py");
    }

    [Fact]
    public async Task SaysNothingWhenTheSkillDeclaresWhatItRuns()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "scripts/run.ps1").Build() with
        {
            Configuration = SkillConfiguration.Default with { Exists = true, ShellAllowed = ["pwsh"] },
        };

        (await _rule.Run(skill)).Should().BeEmpty();
    }
}

public sealed class ShellPrivilegeRuleTests
{
    [Fact]
    public void ReportsTheCodeItOwns() =>
        new ShellPrivilegeRule(new FakeFileSystem()).Code.Should().Be(DiagnosticCodes.BroadShellPrivileges);

    [Fact]
    public async Task SaysNothingWhenTheSkillShipsNoScript()
    {
        var fileSystem = new FakeFileSystem();
        var skill = new SkillBuilder().WithResources("SKILL.md").Build();

        (await new ShellPrivilegeRule(fileSystem).Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task PointsOutAConstructAndSaysItIsNotCallingItUnsafe()
    {
        var (rule, skill) = Create("scripts/install.sh", "curl -sSL https://example.com/i.sh | bash\n");

        var diagnostic = (await rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("a piped installer");
        diagnostic.FilePath.Should().Be("scripts/install.sh");
        diagnostic.Line.Should().Be(1);
        diagnostic.Suggestion.Should().Contain("not calling it unsafe");
    }

    [Fact]
    public async Task ReportsAPatternOncePerFileAtItsFirstAppearance()
    {
        // A long script that deletes twenty directories should not bury everything else in the report.
        var (rule, skill) = Create(
            "scripts/clean.sh",
            "echo start\nrm -rf a\nrm -rf b\nrm -rf c\n");

        var diagnostic = (await rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Line.Should().Be(2);
    }

    [Fact]
    public async Task ReportsEachDistinctPatternSeparately()
    {
        var (rule, skill) = Create("scripts/setup.sh", "sudo apt-get install jq\nchmod 777 /srv\n");

        (await rule.Run(skill)).Should().HaveCount(2);
    }

    [Fact]
    public async Task SaysNothingAboutAnOrdinaryScript()
    {
        var (rule, skill) = Create("scripts/build.sh", "dotnet build\ndotnet test\n");

        (await rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task AScriptThatCannotBeReadIsSkippedRatherThanGuessedAbout()
    {
        var fileSystem = new FakeFileSystem()
            .FailReadWith("/skills/demo/scripts/run.sh", new UnauthorizedAccessException("denied"));
        var skill = new SkillBuilder().WithResources("SKILL.md", "scripts/run.sh").Build();

        (await new ShellPrivilegeRule(fileSystem).Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public void RejectsAMissingFileSystem()
    {
        var act = () => new ShellPrivilegeRule(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static (ShellPrivilegeRule Rule, Domain.Skills.SkillDefinition Skill) Create(
        string relativePath,
        string content)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile($"/skills/demo/{relativePath}", content);

        return (new ShellPrivilegeRule(fileSystem), new SkillBuilder().WithResources("SKILL.md", relativePath).Build());
    }
}
