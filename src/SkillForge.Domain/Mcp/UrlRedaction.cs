namespace SkillForge.Domain.Mcp;

/// <summary>
/// Removes credentials from a URL before it enters the model.
/// </summary>
/// <remarks>
/// <c>https://user:sk-live-abc123@db.example.com/mcp</c> is a URL with a secret in it, and it appears in real
/// configuration files and real registry listings. Every one of SkillForge's outputs prints an endpoint — the
/// console, the JSON, the SARIF, a Mermaid label, an exception message when a probe fails — so a URL carrying
/// user-info would leak the credential into a terminal, a CI log, an uploaded scanning result and a file somebody
/// then pastes into a chat.
///
/// **Redaction happens where the URL is read, not where it is printed.** That is the same rule the environment
/// variable names follow: the value never enters the model, so there is nothing downstream to remember to filter.
/// A printer that forgot would be a leak, and there are eight of them.
///
/// The credential is removed rather than masked. SkillForge does not authenticate against anything, so it has no
/// use for the value, and a <c>***</c> in a report would suggest something is still holding it.
/// </remarks>
public static class UrlRedaction
{
    /// <summary>
    /// The same URL with any user-info removed, or the value unchanged when it is not a URL or carries none.
    /// </summary>
    /// <param name="value">The URL, a command line, or <see langword="null"/>.</param>
    /// <returns>The value with credentials removed.</returns>
    /// <remarks>
    /// A command line is not a URL and is returned untouched — <c>npx -y some-mcp</c> must survive this
    /// unchanged, because the same field on a declaration holds either one.
    /// </remarks>
    public static string? WithoutCredentials(string? value)
    {
        if (value is not { Length: > 0 }
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length == 0)
        {
            return value;
        }

        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.ToString();
    }

    /// <summary>
    /// The same URI with any user-info removed.
    /// </summary>
    /// <param name="uri">The URI, or <see langword="null"/>.</param>
    /// <returns>The URI with credentials removed, or <see langword="null"/>.</returns>
    public static Uri? WithoutCredentials(Uri? uri) =>
        uri is null || !uri.IsAbsoluteUri || uri.UserInfo.Length == 0
            ? uri
            : new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;
}
