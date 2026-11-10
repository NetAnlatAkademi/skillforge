namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// The raw bytes <c>resources/read</c> returned for one URI.
/// </summary>
/// <param name="Uri">The URI that was asked for.</param>
/// <param name="Bytes">The content: <c>text</c> encoded as UTF-8, or <c>blob</c> decoded from base64.</param>
/// <param name="MimeType">The <c>mimeType</c> the server named, when it named one.</param>
public sealed record McpResourceContent(string Uri, ReadOnlyMemory<byte> Bytes, string? MimeType);
