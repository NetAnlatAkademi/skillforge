namespace SkillForge.Application.Validation;

/// <summary>
/// A local file reference found in a Markdown body.
/// </summary>
/// <param name="Target">
/// The referenced path as written, with any anchor and title removed, using <c>/</c> separators.
/// </param>
/// <param name="Line">One-based line of the reference within its file.</param>
public sealed record MarkdownLink(string Target, int Line);
