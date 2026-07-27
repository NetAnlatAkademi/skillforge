using System.IO.Compression;
using System.Text;
using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure.Tests;

public sealed class DeterministicZipArchiveWriterTests
{
    private readonly DeterministicZipArchiveWriter _writer = new();
    private readonly Sha256HashCalculator _hasher = new();

    [Fact]
    public async Task ProducesAReadableArchive()
    {
        var bytes = await _writer.CreateAsync(Entries(), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.Entries.Select(entry => entry.FullName)
            .Should().Equal("SKILL.md", "references/notes.md");
    }

    [Fact]
    public async Task TheSameEntriesProduceByteIdenticalOutput()
    {
        // The whole reason a package hash is worth publishing.
        var first = await _writer.CreateAsync(Entries(), CancellationToken.None);
        var second = await _writer.CreateAsync(Entries(), CancellationToken.None);

        _hasher.ComputeSha256(second).Should().Be(_hasher.ComputeSha256(first));
    }

    [Fact]
    public async Task ChangedContentChangesTheBytes()
    {
        var original = await _writer.CreateAsync(Entries(), CancellationToken.None);
        var changed = await _writer.CreateAsync(Entries("different notes"), CancellationToken.None);

        _hasher.ComputeSha256(changed).Should().NotBe(_hasher.ComputeSha256(original));
    }

    [Fact]
    public async Task EntryTimestampsArePinnedSoTheHashDoesNotDriftBetweenBuilds()
    {
        var bytes = await _writer.CreateAsync(Entries(), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.Entries.Should().AllSatisfy(entry =>
            entry.LastWriteTime.Year.Should().Be(1980));
    }

    [Fact]
    public async Task BackslashesBecomeForwardSlashesSoArchivesAreCrossPlatform()
    {
        var bytes = await _writer.CreateAsync(
            [new ArchiveEntry(@"references\notes.md", Encoding.UTF8.GetBytes("notes"))],
            CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle().Which.FullName.Should().Be("references/notes.md");
    }

    [Fact]
    public async Task AnEmptyEntryListProducesAnEmptyArchiveRatherThanThrowing()
    {
        var bytes = await _writer.CreateAsync([], CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task IsCancellable()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await _writer.CreateAsync(Entries(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RejectsMissingEntries()
    {
        var act = async () => await _writer.CreateAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static ArchiveEntry[] Entries(string notes = "notes") =>
    [
        new("SKILL.md", Encoding.UTF8.GetBytes("---\nname: demo\n---\n")),
        new("references/notes.md", Encoding.UTF8.GetBytes(notes)),
    ];
}
