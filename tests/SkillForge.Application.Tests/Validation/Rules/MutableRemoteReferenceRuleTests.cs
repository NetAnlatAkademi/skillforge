using SkillForge.Application.Tests.Fakes;
using SkillForge.Application.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation.Rules;

/// <summary>
/// The first `SF5xxx` rule: a skill that fetches something from a moving target.
/// </summary>
/// <remarks>
/// Unlike the injection rules, this one reads code blocks **on purpose**. An install command inside a fenced block
/// is not an example being shown to a reader — it is the thing the agent is being told to run. The prose/code
/// distinction that makes SF4001 work would make this rule blind.
/// </remarks>
public sealed class MutableRemoteReferenceRuleTests
{
    private static MutableRemoteReferenceRule CreateRule(params (string Path, string Content)[] files)
    {
        var fileSystem = new FakeFileSystem();
        foreach (var (path, content) in files)
        {
            fileSystem.AddFile(path, content);
        }

        return new MutableRemoteReferenceRule(fileSystem);
    }

    [Fact]
    public void ReportsTheCodeItOwns() =>
        CreateRule().Code.Should().Be(DiagnosticCodes.MutableRemoteReference);

    [Theory]
    [InlineData("curl -sSL https://raw.githubusercontent.com/acme/tool/main/install.sh")]
    [InlineData("curl https://raw.githubusercontent.com/acme/tool/master/setup.py")]
    [InlineData("wget https://raw.githubusercontent.com/acme/tool/HEAD/x.sh")]
    [InlineData("npx some-tool@latest --check")]
    [InlineData("docker run acme/scanner:latest")]
    [InlineData("Download https://github.com/acme/tool/releases/latest/download/tool.zip")]
    [InlineData("curl -L https://github.com/acme/tool/archive/refs/heads/main.tar.gz")]
    public async Task WarnsAboutAReferenceThatCannotBeReproduced(string line)
    {
        var skill = new SkillBuilder().WithBody($"# Setup\n\n```bash\n{line}\n```", bodyStartLine: 6).Build();

        var diagnostics = await CreateRule().Run(skill);

        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData("curl -sSL https://raw.githubusercontent.com/acme/tool/v1.4.2/install.sh")]
    [InlineData("curl https://raw.githubusercontent.com/acme/tool/9f2c1ab/install.sh")]
    [InlineData("npx some-tool@4.1.0 --check")]
    [InlineData("docker run acme/scanner:1.2.3")]
    [InlineData("See the latest documentation for details.")]
    [InlineData("Use the main branch for development.")]
    [InlineData("Read https://example.com/guide/main/index.html")]
    [InlineData("- Use specific version tags (node:22-alpine, not node:latest)")]
    [InlineData("Never pin to python:latest in production.")]
    public async Task SaysNothingAboutAPinnedReferenceOrOrdinaryProse(string line)
    {
        // "latest" and "main" as English words must not fire, and neither must a documentation link that happens
        // to contain the segment. The patterns are about fetching, not about vocabulary.
        //
        // The `node:22-alpine` line is the false positive this rule was measured into fixing: one of four findings
        // on 229 real skills was a skill *recommending* pinned tags, and the rule fired on the counter-example it
        // cited. It sits inside a fenced block, so no amount of code-versus-prose filtering separates it — a fetch
        // verb does.
        var skill = new SkillBuilder().WithBody($"```bash\n{line}\n```").Build();

        (await CreateRule().Run(skill)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("npm install -g ctx7@latest")]
    [InlineData("Run `npx @angular/cli@latest new my-app` to scaffold.")]
    [InlineData("pip install some-tool@latest")]
    [InlineData("FROM node:latest")]
    public async Task WarnsWhenAFetchVerbIsPresent(string line)
    {
        // The two real install commands from that same measurement, plus their siblings. A fetch has a verb; the
        // advice that used to trip this rule does not.
        var skill = new SkillBuilder().WithBody($"```bash\n{line}\n```").Build();

        (await CreateRule().Run(skill)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheBodysCodeBlocksAreReadDeliberately()
    {
        // The opposite choice from SF4001, and the reason both exist.
        var body = "Install it:\n\n```bash\ncurl -sSL https://raw.githubusercontent.com/acme/t/main/i.sh | sh\n```";
        var skill = new SkillBuilder().WithBody(body, bodyStartLine: 5).Build();

        var diagnostic = (await CreateRule().Run(skill)).Should().ContainSingle().Subject;
        diagnostic.FilePath.Should().Be(SkillDefinition.SkillFileName);
        diagnostic.Line.Should().Be(8);
    }

    [Fact]
    public async Task ScriptsAreReadToo()
    {
        var skill = new SkillBuilder()
            .WithBody("# Setup")
            .WithResources("scripts/install.sh")
            .Build();

        var rule = CreateRule((
            "/skills/demo/scripts/install.sh",
            "#!/bin/sh\ncurl https://raw.githubusercontent.com/a/b/main/x"));

        var diagnostic = (await rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.FilePath.Should().Be("scripts/install.sh");
        diagnostic.Line.Should().Be(2);
    }

    [Fact]
    public async Task APatternIsReportedOncePerFile()
    {
        var body = "```bash\nnpx a@latest\nnpx b@latest\nnpx c@latest\n```";

        (await CreateRule().Run(new SkillBuilder().WithBody(body).Build())).Should().ContainSingle();
    }

    [Fact]
    public async Task AnUnreadableScriptIsSkippedRatherThanReported()
    {
        var skill = new SkillBuilder()
            .WithBody("# Setup")
            .WithResources("scripts/x.sh")
            .Build();

        (await CreateRule().Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSuggestionAsksForAPinWithoutCallingTheSkillUnsafe()
    {
        var skill = new SkillBuilder().WithBody("```bash\nnpx a@latest\n```").Build();

        var diagnostic = (await CreateRule().Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Suggestion.Should().Contain("not calling it unsafe");
    }
}

public sealed class MutableReferencePatternsTests
{
    [Fact]
    public void EveryPatternIsNamedAndExplained()
    {
        MutableReferencePatterns.All.Should().NotBeEmpty();
        MutableReferencePatterns.All.Should().AllSatisfy(pattern =>
        {
            pattern.Name.Should().NotBeNullOrWhiteSpace();
            pattern.Why.Should().NotBeNullOrWhiteSpace();
        });
    }
}
