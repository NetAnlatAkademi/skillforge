using System.Text.Json.Nodes;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// Follows a server's <c>nextCursor</c> under SkillForge's three bounds, for any paginated method.
/// </summary>
/// <remarks>
/// <c>tools/list</c>, <c>skills/list</c> and <c>resources/directory/read</c> all page the same way, and they are walked
/// the same way, so a server cannot find a list that one of them bounds and another does not.
///
/// The three bounds: <see cref="MaxPages"/> pages; a stop on a cursor the server already handed out, because the
/// cursor is opaque and repetition is the only progress check it allows; and a cancellation check before every
/// request rather than only at the start. Whichever fires is recorded on the result — a count that stopped early is a
/// floor, never a total.
///
/// A failed page stops the walk and keeps what was read, because it is true. A first page that fails is reported as a
/// complete walk of nothing, with the error alongside: nothing was read, so nothing was cut short, but the caller can
/// still say why.
/// </remarks>
internal static class McpCursorWalk
{
    /// <summary>
    /// How many pages one walk asks for. Not configurable, deliberately: a limit nobody has needed to change is not a
    /// setting, and reaching it is reported rather than silently absorbed.
    /// </summary>
    internal const int MaxPages = 100;

    /// <summary>Walks every page.</summary>
    /// <typeparam name="T">What one page carries.</typeparam>
    /// <param name="fetch">Asks for one page: the cursor (absent on the first request) and the one-based page number.</param>
    /// <param name="maxPages">The page bound; <see cref="MaxPages"/> unless a caller has a tighter one.</param>
    /// <param name="cancellationToken">Checked before every request.</param>
    /// <returns>Every page read, how the walk ended, and the error that ended it, when one did.</returns>
    internal static async Task<CursorWalk<T>> WalkAsync<T>(
        Func<string?, int, CancellationToken, Task<McpResponse<CursorPage<T>>>> fetch,
        int maxPages = MaxPages,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(fetch);

        var pages = new List<T>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await fetch(cursor, pages.Count + 1, cancellationToken).ConfigureAwait(false);

            if (response.Value is not { } page)
            {
                return new CursorWalk<T>(
                    pages,
                    pages.Count == 0 ? McpToolPagingOutcome.Complete : McpToolPagingOutcome.PageLimitReached,
                    response.Error);
            }

            pages.Add(page.Items);

            if (page.NextCursor is not { Length: > 0 } next)
            {
                return new CursorWalk<T>(pages, McpToolPagingOutcome.Complete, null);
            }

            if (!seenCursors.Add(next))
            {
                return new CursorWalk<T>(pages, McpToolPagingOutcome.CursorLoopDetected, null);
            }

            if (pages.Count >= maxPages)
            {
                return new CursorWalk<T>(pages, McpToolPagingOutcome.PageLimitReached, null);
            }

            cursor = next;
        }
    }

    /// <summary>
    /// The cursor that asks for the next page, or <see langword="null"/> when there is none. An empty string is treated
    /// as absent: a server that sends <c>"nextCursor": ""</c> has ended the list, and asking for page after page of
    /// nothing would be a loop the repeat guard never sees.
    /// </summary>
    /// <param name="result">The <c>result</c> node of a paginated response.</param>
    /// <returns>The cursor, or <see langword="null"/>.</returns>
    internal static string? NextCursor(JsonNode? result) =>
        result?["nextCursor"] is JsonValue value
            && value.TryGetValue<string>(out var cursor)
            && cursor.Length > 0
                ? cursor
                : null;
}
