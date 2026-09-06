using SkillForge.Application.Provenance;
using SkillForge.Application.Tests.Fakes;

namespace SkillForge.Application.Tests.Provenance;

/// <summary>
/// A fingerprint's only job is to change when the asset does, and not otherwise.
/// </summary>
public sealed class AssetFingerprinterTests
{
    [Fact]
    public async Task TheSameContentsProduceTheSameFingerprint()
    {
        var first = await Fingerprint(Layout());
        var second = await Fingerprint(Layout());

        first.Should().Be(second);
    }

    [Fact]
    public async Task ChangedContentChangesTheFingerprint()
    {
        var original = await Fingerprint(Layout());
        var edited = await Fingerprint(Layout(body: "# Demo, but different"));

        edited.Should().NotBe(original);
    }

    [Fact]
    public async Task AddingAFileChangesTheFingerprint()
    {
        var layout = Layout().AddFile("/repo/demo/references/extra.md", "notes");

        (await Fingerprint(layout)).Should().NotBe(await Fingerprint(Layout()));
    }

    [Fact]
    public async Task RenamingAFileChangesTheFingerprintEvenWhenItsBytesDoNot()
    {
        // Path is part of the hashed line, so a move is a change. A fingerprint that ignored paths would call two
        // different layouts identical.
        var moved = new FakeFileSystem()
            .AddFile("/repo/demo/SKILL.md", "# Demo")
            .AddFile("/repo/demo/notes/checklist.md", "one");

        var original = new FakeFileSystem()
            .AddFile("/repo/demo/SKILL.md", "# Demo")
            .AddFile("/repo/demo/references/checklist.md", "one");

        (await Fingerprint(moved)).Should().NotBe(await Fingerprint(original));
    }

    [Fact]
    public async Task BuildOutputDoesNotContributeToTheFingerprint()
    {
        // Otherwise running a build would change the identity of the asset that was built.
        var withBuildOutput = Layout()
            .AddFile("/repo/demo/bin/Debug/demo.dll", "binary")
            .AddFile("/repo/demo/.git/HEAD", "ref: refs/heads/main");

        (await Fingerprint(withBuildOutput)).Should().Be(await Fingerprint(Layout()));
    }

    [Fact]
    public async Task AnUnreadableFileStillCountsTowardsTheFingerprint()
    {
        // Skipping it would let the contents of an asset change without its fingerprint changing, which is the one
        // thing a fingerprint must never allow.
        var unreadable = Layout().FailReadWith("/repo/demo/references/locked.md", new IOException("locked"));

        (await Fingerprint(unreadable)).Should().NotBe(await Fingerprint(Layout()));
    }

    [Fact]
    public async Task AFileTooLargeToReadStillMovesTheFingerprintWhenItsSizeChanges()
    {
        // The contents of a fingerprinted tree came from somewhere else, so reading a file whole is a decision
        // about how much memory a stranger gets to allocate. Past the cap the size is hashed instead, which keeps
        // a swapped file of a different length visible.
        var big = Layout().AddFile("/repo/demo/assets/model.bin", new string('x', 11 * 1024 * 1024));
        var bigger = Layout().AddFile("/repo/demo/assets/model.bin", new string('x', 12 * 1024 * 1024));

        (await Fingerprint(big)).Should().NotBe(await Fingerprint(bigger));
        (await Fingerprint(big)).Should().NotBe(await Fingerprint(Layout()));
    }

    [Fact]
    public async Task AnEmptyDirectoryStillHasAFingerprint()
    {
        var empty = new FakeFileSystem().AddDirectory("/repo/empty");

        (await Fingerprint(empty, "/repo/empty")).Should().NotBeNullOrEmpty();
    }

    private static FakeFileSystem Layout(string body = "# Demo") =>
        new FakeFileSystem()
            .AddFile("/repo/demo/SKILL.md", body)
            .AddFile("/repo/demo/references/checklist.md", "one");

    private static async Task<string> Fingerprint(FakeFileSystem fileSystem, string directory = "/repo/demo") =>
        await new AssetFingerprinter(fileSystem, new FakeHashCalculator())
            .ComputeAsync(directory, CancellationToken.None);
}
