using SkillForge.Domain.Mcp;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>A whole walk.</summary>
/// <typeparam name="T">What each page carries.</typeparam>
/// <param name="Pages">Every page read, in order.</param>
/// <param name="Outcome">How the walk ended.</param>
/// <param name="Error">The error that ended it, when one did.</param>
internal sealed record CursorWalk<T>(IReadOnlyList<T> Pages, McpToolPagingOutcome Outcome, string? Error);
