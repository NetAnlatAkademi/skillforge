using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Provenance;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Provenance;

namespace SkillForge.Infrastructure.Provenance;

/// <summary>
/// Reads plugin and marketplace manifests from JSON.
/// </summary>
/// <remarks>
/// Comments and trailing commas are allowed, for the same reason <see cref="Migration.JsonMcpConfigurationReader"/>
/// allows them: real files in real home directories have both, and a strict parser reports a working configuration
/// as corrupt.
///
/// Every field is optional. A manifest that names only a plugin still produces a manifest, with the rest unset —
/// the alternative is a parser that refuses a file the tool that owns it accepts.
///
/// **Nothing here reads a value that could be a credential.** Neither file is a likely place for one, but a
/// marketplace entry can carry authentication settings for a private source, and this reader takes the fields it
/// names and nothing else.
/// </remarks>
public sealed class JsonDistributionManifestReader : IDistributionManifestReader
{
    private static readonly JsonDocumentOptions ForgivingJson = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Property names a publisher can appear under, in the order they are trusted.</summary>
    private static readonly string[] PublisherFields = ["author", "owner", "publisher", "maintainer"];

    /// <summary>Property names a repository URL can appear under.</summary>
    private static readonly string[] RepositoryFields = ["repository", "repo", "url", "source", "homepage"];

    /// <summary>Property names a revision can appear under.</summary>
    private static readonly string[] RevisionFields = ["commit", "sha", "ref", "rev", "branch", "tag"];

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">Used to read the files.</param>
    public JsonDistributionManifestReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<OperationResult<PluginManifest>> ReadPluginAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var (root, failure) = await ParseAsync<PluginManifest>(path, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return OperationResult<PluginManifest>.Success(new PluginManifest(
            Text(root, "name") ?? DirectoryNameOf(path),
            Text(root, "version"),
            FirstText(root, PublisherFields),
            FirstText(root, RepositoryFields),
            path));
    }

    /// <inheritdoc />
    public async Task<OperationResult<MarketplaceListing>> ReadMarketplaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var (root, failure) = await ParseAsync<MarketplaceListing>(path, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return OperationResult<MarketplaceListing>.Success(new MarketplaceListing(
            Text(root, "name") ?? DirectoryNameOf(path),
            FirstText(root, PublisherFields),
            path,
            ReadEntries(root)));
    }

    /// <summary>
    /// Reads and parses the file, returning either the root element or the failure to report.
    /// </summary>
    /// <remarks>
    /// The parsed document is disposed here and the element is read from a clone, because a
    /// <see cref="JsonElement"/> outliving its document is a use-after-free that only shows up under load. These
    /// files are a few kilobytes; cloning them costs nothing worth measuring.
    /// </remarks>
    private async Task<(JsonElement Root, OperationResult<T>? Failure)> ParseAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        string content;

        try
        {
            content = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (default, Unreadable<T>(path, exception.Message));
        }

        try
        {
            using var document = JsonDocument.Parse(content, ForgivingJson);

            return document.RootElement.ValueKind is JsonValueKind.Object
                ? (document.RootElement.Clone(), null)
                : (default, Unreadable<T>(path, "its root is not a JSON object"));
        }
        catch (JsonException exception)
        {
            return (default, Unreadable<T>(path, exception.Message));
        }
    }

    /// <summary>
    /// Reads the listed plugins. Both shapes seen in the wild are accepted: an array of objects, and an array of
    /// bare names for a marketplace whose plugins live beside it.
    /// </summary>
    private static List<MarketplaceEntry> ReadEntries(JsonElement root)
    {
        if (!root.TryGetProperty("plugins", out var plugins) || plugins.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<MarketplaceEntry>();

        foreach (var plugin in plugins.EnumerateArray())
        {
            if (plugin.ValueKind is JsonValueKind.String)
            {
                entries.Add(new MarketplaceEntry(
                    plugin.GetString() ?? string.Empty,
                    null,
                    null,
                    null,
                    null,
                    null));

                continue;
            }

            if (plugin.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            var source = plugin.TryGetProperty("source", out var nested) && nested.ValueKind is JsonValueKind.Object
                ? nested
                : plugin;

            entries.Add(new MarketplaceEntry(
                Text(plugin, "name") ?? string.Empty,
                FirstText(source, RepositoryFields) ?? FirstText(plugin, RepositoryFields),
                FirstText(source, RevisionFields) ?? FirstText(plugin, RevisionFields),
                Text(plugin, "version") ?? Text(source, "version"),
                Flag(plugin, "autoUpdate") ?? Flag(plugin, "automaticUpdates") ?? Flag(source, "autoUpdate"),
                Text(plugin, "updateMode") ?? Text(source, "updateMode")));
        }

        return entries;
    }

    /// <summary>
    /// Reads a string property. An object is accepted too, and its <c>name</c>, <c>url</c> or <c>login</c> read —
    /// <c>"author": "kim"</c> and <c>"author": { "name": "kim" }</c> are both common and both mean the same thing.
    /// </summary>
    private static string? Text(JsonElement element, string property)
    {
        if (element.ValueKind is not JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => Trimmed(value.GetString()),
            JsonValueKind.Object => Trimmed(
                Text(value, "name") ?? Text(value, "url") ?? Text(value, "login") ?? Text(value, "repo")),
            _ => null,
        };
    }

    private static string? FirstText(JsonElement element, IReadOnlyList<string> properties)
    {
        foreach (var property in properties)
        {
            if (Text(element, property) is { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads a boolean property, or <see langword="null"/> when the file does not state it.</summary>
    private static bool? Flag(JsonElement element, string property)
    {
        if (element.ValueKind is not JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? Trimmed(string? value) =>
        value is { Length: > 0 } && value.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    /// <summary>Names the manifest after its directory when it does not name itself.</summary>
    private static string DirectoryNameOf(string path)
    {
        var directory = Path.GetDirectoryName(path);

        // .claude-plugin/plugin.json describes the directory above it, not the metadata directory itself.
        var parent = directory is { Length: > 0 } ? Path.GetDirectoryName(directory) : null;

        return Path.GetFileName(parent ?? directory ?? path) is { Length: > 0 } name
            ? name
            : Path.GetFileNameWithoutExtension(path);
    }

    private static OperationResult<T> Unreadable<T>(string path, string reason) =>
        OperationResult<T>.Failure(Diagnostic.Warning(
            DiagnosticCodes.ProviderConfigurationNotParsable,
            $"'{path}' could not be read, so what it declares is missing from this report: {reason}",
            path,
            suggestion: "Check the file with the tool that owns it. SkillForge reports what it cannot read rather "
                + "than presenting an incomplete report as a complete one."));
}
