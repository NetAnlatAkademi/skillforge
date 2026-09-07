namespace SkillForge.Domain.Graph;

/// <summary>
/// How sure the graph is about an edge.
/// </summary>
/// <remarks>
/// Two levels, and the line between them is whether a machine wrote the claim down or a person's prose implied it.
/// A third — observed at runtime — is not here, because nothing in the graph command talks to anything: what a live
/// server actually exposes is <c>discovery verify</c>'s question, and answering it on a diagram would let a drawing
/// imply a probe that never happened.
/// </remarks>
public enum GraphConfidence
{
    /// <summary>
    /// Read from a structured field: an MCP server's URL, a plugin manifest, a skill's <c>allowed-tools</c>, a file
    /// inside a directory. The claim is the file's, not SkillForge's.
    /// </summary>
    Declared = 0,

    /// <summary>
    /// Drawn from a name or from prose: a server's name appearing in a skill's body, an environment variable whose
    /// name looks like a credential. The evidence is exact — a file and a line — and the conclusion is a heuristic,
    /// which is why it is labelled everywhere it is printed.
    /// </summary>
    Inferred,
}

/// <summary>
/// Where a claim was read.
/// </summary>
/// <remarks>
/// Every edge carries one. The rule the graph is built under is <strong>no evidence, no claim</strong>: an edge that
/// cannot say which file and, where the file has lines, which line, is not emitted at all. That is what makes the
/// graph checkable rather than merely plausible — a reader can open the file and disagree.
/// </remarks>
/// <param name="File">
/// Path of the file the claim was read from, relative to the scanned root and using <c>/</c> separators so that a
/// report is identical on Windows and Linux.
/// </param>
/// <param name="Line">
/// One-based line the claim was read at, or <see langword="null"/> when the evidence is the file's existence or its
/// location rather than something written in it.
/// </param>
/// <param name="Kind">
/// What sort of evidence it is, in a few words: <c>mcp server url</c>, <c>skill body mention</c>,
/// <c>allowed-tools entry</c>. Written for a person reading a report, not parsed by anything.
/// </param>
public sealed record GraphEvidence(string File, int? Line, string Kind);
