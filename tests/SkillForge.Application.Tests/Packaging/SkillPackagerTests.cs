using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Packaging;
using SkillForge.Application.Provenance;
using SkillForge.Application.Tests.Fakes;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Packaging;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Packaging;

public sealed class SkillPackagerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WritesAnArchiveAHashFileAndAManifest()
    {
        var fileSystem = Files();
        var result = await Pack(fileSystem);

        result.IsSuccess.Should().BeTrue();
        var package = result.Value!;
        package.ArchivePath.Should().EndWith("demo-skill.1.0.0.skill.zip");
        package.HashPath.Should().EndWith("demo-skill.1.0.0.skill.zip.sha256");
        package.ManifestPath.Should().EndWith("demo-skill.1.0.0.manifest.json");
        fileSystem.FileExists(package.ArchivePath).Should().BeTrue();
        fileSystem.FileExists(package.HashPath).Should().BeTrue();
        fileSystem.FileExists(package.ManifestPath).Should().BeTrue();
    }

    [Fact]
    public async Task TheSameContentsProduceTheSameHash()
    {
        // The point of publishing a hash is that somebody else can reproduce it.
        var first = await Pack(Files());
        var second = await Pack(Files());

        second.Value!.ArchiveSha256.Should().Be(first.Value!.ArchiveSha256);
    }

    [Fact]
    public async Task DifferentContentsProduceADifferentHash()
    {
        var changed = Files();
        await changed.WriteAllTextAsync("/skills/demo/references/notes.md", "different", CancellationToken.None);

        var original = await Pack(Files());
        var modified = await Pack(changed);

        modified.Value!.ArchiveSha256.Should().NotBe(original.Value!.ArchiveSha256);
    }

    [Fact]
    public async Task FilesArePackagedInAStableOrderWithTheirOwnHashes()
    {
        var result = await Pack(Files());

        result.Value!.Files.Select(file => file.RelativePath)
            .Should().Equal("SKILL.md", "references/notes.md");
        result.Value.Files.Should().AllSatisfy(file =>
        {
            file.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
            file.SizeInBytes.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task ToolingDirectoriesAreNeverPackaged()
    {
        var skill = new SkillBuilder()
            .WithResources("SKILL.md", "bin/tool.dll", "obj/temp.txt", ".git/config", "references/notes.md")
            .Build();

        var result = await Pack(Files(), skill);

        result.Value!.Files.Select(file => file.RelativePath)
            .Should().Equal("SKILL.md", "references/notes.md");
    }

    [Fact]
    public async Task TheManifestRecordsTheSkillTheHashAndTheFiles()
    {
        var fileSystem = Files();
        var result = await Pack(fileSystem);

        using var manifest = JsonDocument.Parse(fileSystem.ReadText(result.Value!.ManifestPath));
        var root = manifest.RootElement;

        root.GetProperty("skill").GetProperty("name").GetString().Should().Be("demo-skill");
        root.GetProperty("skill").GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("package").GetProperty("sha256").GetString()
            .Should().Be(result.Value.ArchiveSha256);
        root.GetProperty("package").GetProperty("createdAt").GetString()
            .Should().Be("2026-07-26T12:00:00Z");
        root.GetProperty("files").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task TheManifestRecordsWhereTheSkillCameFrom()
    {
        var fileSystem = Files();

        var result = await Create(fileSystem, new SkillProvenance(
            "https://github.com/example/skills.git",
            "abc123def4567890abc123def4567890abc123de",
            "skills/demo",
            false,
            "26.215.1",
            FixedNow)).PackAsync(
                new SkillBuilder().WithResources("SKILL.md", "references/notes.md").Build(),
                "/out",
                null,
                CancellationToken.None);

        var source = JsonDocument.Parse(fileSystem.ReadText(result.Value!.ManifestPath))
            .RootElement.GetProperty("source");

        source.GetProperty("repository").GetString().Should().Be("https://github.com/example/skills.git");
        source.GetProperty("commit").GetString().Should().Be("abc123def4567890abc123def4567890abc123de");
        source.GetProperty("path").GetString().Should().Be("skills/demo");
        source.GetProperty("workingTreeIsDirty").GetBoolean().Should().BeFalse();
        source.GetProperty("generatedAt").GetString().Should().Be("2026-07-26T12:00:00Z");
    }

    [Fact]
    public async Task AnUnknownSourceIsWrittenAsNullRatherThanOmitted()
    {
        // A key that disappears cannot be told apart from a manifest written before provenance existed.
        var fileSystem = Files();
        var result = await Pack(fileSystem);

        var source = JsonDocument.Parse(fileSystem.ReadText(result.Value!.ManifestPath))
            .RootElement.GetProperty("source");

        source.GetProperty("repository").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("commit").ValueKind.Should().Be(JsonValueKind.Null);
        source.GetProperty("path").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task TheManifestNamesTheToolVersionThatProducedIt()
    {
        var fileSystem = Files();
        var result = await Pack(fileSystem);

        var tool = JsonDocument.Parse(fileSystem.ReadText(result.Value!.ManifestPath))
            .RootElement.GetProperty("tool");

        tool.GetProperty("name").GetString().Should().Be("SkillForge");
        tool.GetProperty("version").GetString().Should().Be("26.215.1");
    }

    [Fact]
    public async Task TheHashFileMatchesTheFormatSha256sumExpects()
    {
        var fileSystem = Files();
        var result = await Pack(fileSystem);

        fileSystem.ReadText(result.Value!.HashPath)
            .Should().StartWith(result.Value.ArchiveSha256 + "  demo-skill.1.0.0.skill.zip");
    }

    [Fact]
    public async Task AnOverrideWinsOverTheDeclaredVersion()
    {
        var result = await Pack(Files(), versionOverride: "2.5.0");

        result.Value!.Version.Should().Be("2.5.0");
        result.Value.ArchivePath.Should().Contain("demo-skill.2.5.0");
    }

    [Fact]
    public async Task ASkillWithNoDeclaredVersionFallsBackToZeroOneZero()
    {
        var skill = new SkillBuilder().WithVersion(null).WithResources("SKILL.md").Build();

        var result = await Pack(Files(), skill);

        result.Value!.Version.Should().Be("0.1.0");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("1.0.0/x")]
    [InlineData("with space")]
    public async Task AVersionThatCannotBeAFileNameIsRefused(string version)
    {
        var result = await Pack(Files(), versionOverride: version);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.PackageVersionInvalid);
    }

    [Fact]
    public async Task ASkillWithNoFilesIsRefusedRatherThanProducingAnEmptyArchive()
    {
        var skill = new SkillBuilder().WithResources().Build();

        var result = await Pack(Files(), skill);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.SkillFileNotFound);
    }

    [Fact]
    public async Task RejectsMissingArguments()
    {
        var packager = Create(Files());
        var skill = new SkillBuilder().Build();

        var noSkill = async () => await packager.PackAsync(null!, "/out", null, CancellationToken.None);
        var noOutput = async () => await packager.PackAsync(skill, " ", null, CancellationToken.None);

        await noSkill.Should().ThrowAsync<ArgumentNullException>();
        await noOutput.Should().ThrowAsync<ArgumentException>();
    }

    private static FakeFileSystem Files()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile("/skills/demo/SKILL.md", "---\nname: demo-skill\n---\n# Demo\n");
        fileSystem.AddFile("/skills/demo/references/notes.md", "notes");
        fileSystem.AddFile("/skills/demo/bin/tool.dll", "binary");
        fileSystem.AddFile("/skills/demo/obj/temp.txt", "temp");
        fileSystem.AddFile("/skills/demo/.git/config", "config");
        return fileSystem;
    }

    private static SkillPackager Create(FakeFileSystem fileSystem, SkillProvenance? provenance = null) =>
        new(
            fileSystem,
            new InMemoryArchiveWriter(),
            new FakeHashCalculator(),
            new FakeTimeProvider(FixedNow),
            new StubProvenanceReader(provenance ?? UnknownSource));

    /// <summary>What a skill packaged from a plain directory has: a tool version, a time, and nothing else.</summary>
    private static SkillProvenance UnknownSource { get; } =
        new(null, null, null, false, "26.215.1", FixedNow);

    private sealed class StubProvenanceReader(SkillProvenance provenance) : IProvenanceReader
    {
        public ValueTask<SkillProvenance> ReadAsync(
            string skillDirectory,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(provenance);
    }

    private static Task<OperationResult<SkillPackage>> Pack(
        FakeFileSystem fileSystem,
        SkillDefinition? skill = null,
        string? versionOverride = null) =>
        Create(fileSystem).PackAsync(
            skill ?? new SkillBuilder().WithResources("SKILL.md", "references/notes.md").Build(),
            "/out",
            versionOverride,
            CancellationToken.None);

    /// <summary>
    /// Concatenates entries instead of zipping them. The real determinism of the ZIP format is the
    /// Infrastructure implementation's concern; here the interest is that the packager feeds it a stable,
    /// sorted set of entries.
    /// </summary>
    private sealed class InMemoryArchiveWriter : IArchiveWriter
    {
        public Task<byte[]> CreateAsync(
            IReadOnlyList<ArchiveEntry> entries,
            CancellationToken cancellationToken)
        {
            var bytes = new List<byte>();
            foreach (var entry in entries)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(entry.RelativePath));
                bytes.AddRange(entry.Content);
            }

            return Task.FromResult(bytes.ToArray());
        }
    }

    /// <summary>A stand-in hash that is still content-dependent, so equality assertions mean something.</summary>
    private sealed class FakeHashCalculator : IHashCalculator
    {
        public string ComputeSha256(ReadOnlySpan<byte> content)
        {
            var hash = 1469598103934665603UL;
            foreach (var b in content)
            {
                hash = (hash ^ b) * 1099511628211UL;
            }

            return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture).PadLeft(64, '0');
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
