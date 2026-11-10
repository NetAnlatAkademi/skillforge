namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// An answer from a server, or the reason there is none.
/// </summary>
/// <remarks>
/// A server that refuses, times out or answers nonsense is a fact about that server, not an exception: one skill that
/// cannot be read must not abandon the report on every other one.
/// </remarks>
/// <typeparam name="T">What a successful answer carries.</typeparam>
/// <param name="Value">The answer, when there is one.</param>
/// <param name="Error">Why there is none, in the server's or the transport's own words.</param>
public sealed record McpResponse<T>(T? Value, string? Error)
    where T : class
{
    /// <summary>An answer.</summary>
    /// <param name="value">What it carried.</param>
    /// <returns>The response.</returns>
    public static McpResponse<T> Ok(T value) => new(value, null);

    /// <summary>No answer.</summary>
    /// <param name="error">Why.</param>
    /// <returns>The response.</returns>
    public static McpResponse<T> Failed(string error) => new(null, error);
}
