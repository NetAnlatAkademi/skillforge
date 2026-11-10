namespace SkillForge.Infrastructure.Mcp;

/// <summary>One page as it came back: what it carried, and the cursor for the next one when there is one.</summary>
/// <typeparam name="T">What the page carries.</typeparam>
/// <param name="Items">The page's content.</param>
/// <param name="NextCursor">The cursor for the next page.</param>
internal sealed record CursorPage<T>(T Items, string? NextCursor);
