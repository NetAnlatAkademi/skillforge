namespace SkillForge.Domain.Discovery;

/// <summary>
/// Decides whether a field's <em>name</em> says its value is a credential.
/// </summary>
/// <remarks>
/// The discovery adapters preserve every field a registry sent, because dropping the ones they do not recognise
/// would lose exactly the evidence a person judging a listing wants. That policy collides with a plain fact about
/// real listings: some of them carry an `apiKey`, an `authorization` or a `token`, with the value in it.
///
/// So the **name** is preserved and the value is withheld. "This listing carries an apiKey field" is the evidence;
/// the value is not evidence of anything and reaches a console, a JSON report and possibly a CI log.
///
/// **Matching is on whole name segments, not substrings.** A substring rule would withhold `keywords` because it
/// contains "key" and `author` because it contains "auth" — and a report that redacts a description is a report
/// people stop reading. The name is split on non-alphanumeric characters and on camel-case boundaries, and each
/// segment is compared whole: `apiKey` splits to `api` and `key` and is withheld; `keywords` stays one segment and
/// is kept.
/// </remarks>
public static class SecretFieldNames
{
    /// <summary>What a report prints in place of a withheld value.</summary>
    public const string Withheld = "(value withheld: the field name says it is a credential)";

    /// <summary>
    /// Name segments that mean the value is a secret.
    /// </summary>
    /// <remarks>
    /// Deliberately in this file, where it can be read and argued with. Every entry is a word that names a
    /// credential rather than describing one — <c>key</c> is here and <c>keyword</c> is not, <c>auth</c> is here
    /// and <c>author</c> is not, which is only workable because matching is segment-exact.
    /// </remarks>
    private static readonly HashSet<string> SecretWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "key", "keys", "apikey", "token", "tokens", "secret", "secrets", "password", "passwd", "pwd",
        "credential", "credentials", "auth", "authorization", "authentication", "bearer", "session",
        "cookie", "signature", "sig", "pat", "pin", "passphrase", "privatekey",
    };

    /// <summary>
    /// Whether a field name says its value is a credential.
    /// </summary>
    /// <param name="name">The field name as the source spelled it.</param>
    /// <returns><see langword="true"/> when the value should be withheld.</returns>
    public static bool IsSecretName(string? name)
    {
        if (name is not { Length: > 0 })
        {
            return false;
        }

        foreach (var segment in Segments(name))
        {
            if (SecretWords.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits a field name into its parts: on anything that is not a letter or digit, and at each transition from
    /// a lower-case letter or digit to an upper-case one.
    /// </summary>
    private static IEnumerable<string> Segments(string name)
    {
        var start = 0;

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];

            if (!char.IsLetterOrDigit(character))
            {
                if (index > start)
                {
                    yield return name[start..index];
                }

                start = index + 1;
                continue;
            }

            if (index > start && char.IsUpper(character) && !char.IsUpper(name[index - 1]))
            {
                yield return name[start..index];
                start = index;
            }
        }

        if (start < name.Length)
        {
            yield return name[start..];
        }
    }
}
