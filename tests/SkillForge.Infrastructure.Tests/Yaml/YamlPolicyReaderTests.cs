using SkillForge.Domain.Diagnostics;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests.Yaml;

/// <summary>
/// A policy is the organisation's decision, so this reader fails loudly where the skill configuration reader
/// forgives: a build that passes because the rules did not load is the worst outcome available.
/// </summary>
public sealed class YamlPolicyReaderTests : IDisposable
{
    private readonly YamlPolicyReader _reader = new(new FileSystem());
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-policy-tests",
        Guid.NewGuid().ToString("n"));

    public YamlPolicyReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadsTheDocumentedSchema()
    {
        var path = Write("""
            schemaVersion: 1

            rules:
              permissions:
                shell:
                  allowed: false

                filesystem:
                  write:
                    allowed:
                      - "./reports/**"

                network:
                  allowedDomains:
                    - "api.github.com"
                    - "learn.microsoft.com"

              provenance:
                requireCommitSha: true
                requirePackageHash: true

              skills:
                requireLicense: true
                maxSkillFileLines: 500

              mcp:
                allowedProtocolVersions:
                  - "2025-11-25"
                  - "2026-07-28"

                denyDeprecatedCapabilities: true
            """);

        var policy = (await _reader.ReadAsync(path)).Value!;

        policy.SchemaVersion.Should().Be(1);
        policy.Permissions.ShellAllowed.Should().BeFalse();
        policy.Permissions.FilesystemWriteAllowed.Should().BeTrue();
        policy.Permissions.FilesystemWritePaths.Should().Equal("./reports/**");
        policy.Permissions.AllowedDomains.Should().Equal("api.github.com", "learn.microsoft.com");
        policy.Provenance.RequireCommitSha.Should().BeTrue();
        policy.Provenance.RequirePackageHash.Should().BeTrue();
        policy.Skills.RequireLicense.Should().BeTrue();
        policy.Skills.MaxSkillFileLines.Should().Be(500);
        policy.Mcp!.AllowedProtocolVersions.Should().Equal("2025-11-25", "2026-07-28");
        policy.Mcp.DenyDeprecatedCapabilities.Should().BeTrue();
    }

    [Fact]
    public async Task ADomainListThatIsAbsentIsSilenceAndAnEmptyOneIsADecision()
    {
        var silent = Write("rules:\n  skills:\n    requireLicense: true\n", "silent.yaml");
        var empty = Write("rules:\n  permissions:\n    network:\n      allowedDomains: []\n", "empty.yaml");

        (await _reader.ReadAsync(silent)).Value!.Permissions.AllowedDomains.Should().BeNull();
        (await _reader.ReadAsync(empty)).Value!.Permissions.AllowedDomains.Should().BeEmpty();
    }

    [Fact]
    public async Task FilesystemWriteAcceptsABooleanAsWellAsAListOfPaths()
    {
        var path = Write("rules:\n  permissions:\n    filesystem:\n      write:\n        allowed: false\n");

        var policy = (await _reader.ReadAsync(path)).Value!;

        policy.Permissions.FilesystemWriteAllowed.Should().BeFalse();
        policy.Permissions.FilesystemWritePaths.Should().BeEmpty();
    }

    [Fact]
    public async Task AFileWithoutTheRulesWrapperIsStillRead()
    {
        var path = Write("permissions:\n  shell:\n    allowed: false\n");

        (await _reader.ReadAsync(path)).Value!.Permissions.ShellAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task AnEmptyFileDecidesNothingAndIsNotAnError()
    {
        var path = Write("\n");

        var result = await _reader.ReadAsync(path);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.ShellAllowed.Should().BeNull();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task BrokenYamlFailsRatherThanFallingBackToNoPolicy()
    {
        var path = Write("rules:\n  permissions:\n   - shell: [unclosed\n");

        var result = await _reader.ReadAsync(path);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyNotParsable);
    }

    [Fact]
    public async Task AMissingFileIsAFailureBecauseAskingForOneIsNotTheSameAsNotAskingForOne()
    {
        var result = await _reader.ReadAsync(Path.Combine(_root, "absent.yaml"));

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicyNotParsable);
    }

    [Fact]
    public async Task ASuppressionWithAReasonIsApplied()
    {
        var path = Write("""
            rules:
              permissions:
                shell:
                  allowed: false

            suppress:
              - code: SF9002
                skill: dotnet-api-review
                reason: "approved in TICKET-123"
            """);

        var result = await _reader.ReadAsync(path);

        result.Diagnostics.Should().BeEmpty();
        var suppression = result.Value!.Suppressions.Should().ContainSingle().Subject;
        suppression.Code.Should().Be("SF9002");
        suppression.Skill.Should().Be("dotnet-api-review");
        suppression.Reason.Should().Be("approved in TICKET-123");
        result.Value.Suppresses("SF9002", "dotnet-api-review").Should().BeTrue();
        result.Value.Suppresses("SF9002", "another-skill").Should().BeFalse();
    }

    [Fact]
    public async Task ASuppressionWithNoReasonIsRefusedAndReported()
    {
        var path = Write("""
            suppress:
              - code: SF9002
            """);

        var result = await _reader.ReadAsync(path);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Suppressions.Should().BeEmpty("a suppression that records no decision is not a decision");
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PolicySuppressionWithoutReason);
    }

    [Fact]
    public async Task AnUnscopedSuppressionAppliesToEverySkill()
    {
        var path = Write("suppress:\n  - code: SF9006\n    reason: \"licences are handled centrally\"\n");

        var policy = (await _reader.ReadAsync(path)).Value!;

        policy.Suppresses("SF9006", "anything-at-all").Should().BeTrue();
    }

    [Fact]
    public async Task RejectsAnEmptyPath()
    {
        var act = async () => await _reader.ReadAsync(" ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private string Write(string content, string fileName = "policy.yaml")
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
