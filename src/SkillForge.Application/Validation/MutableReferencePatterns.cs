using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// References to something remote that can change under the skill's feet.
/// </summary>
/// <remarks>
/// The supply-chain question a skill can actually be asked from its own text: *if I run this tomorrow, do I get
/// the same thing?* A URL pointing at a branch, a tag called `latest`, or a container image with no version all
/// answer no. That is not a vulnerability — it is the property that turns somebody else's compromise into yours,
/// and it is invisible unless something says it out loud.
///
/// Every pattern requires the **fetch context**, not the vocabulary. "See the latest documentation" and "use the
/// main branch for development" are ordinary English about ordinary things, and matching them would make this rule
/// noise on every well-written skill. So `latest` only counts next to `@` or `:` or inside a release path, and
/// `main` only counts as a path segment of a raw-content or archive URL.
///
/// Signals, never verdicts (ADR-006). Pinning is a trade-off, not a law: a skill that deliberately tracks a
/// moving upstream has made a choice, and this rule's job is to make sure it was a choice.
/// </remarks>
public static partial class MutableReferencePatterns
{
    /// <summary>Every mutable-reference pattern SkillForge recognises.</summary>
    public static IReadOnlyList<RiskPattern> All { get; } =
    [
        new(
            "a raw file URL pinned to a branch rather than a commit",
            RawContentBranch(),
            "the file at that URL can change without the skill changing, so what the agent runs tomorrow is "
                + "whatever the branch holds then — pin a commit or a tag instead"),
        new(
            "a source archive taken from a branch",
            ArchiveFromBranch(),
            "a branch archive is rebuilt whenever the branch moves, so its contents and its checksum are both "
                + "unstable"),
        new(
            "a package or image resolved to \"latest\"",
            LatestVersion(),
            "\"latest\" is whatever the registry decides it is at the moment of the call, which makes the skill "
                + "unreproducible and hands version selection to somebody else"),
        new(
            "a release download that follows \"latest\"",
            LatestRelease(),
            "the artefact behind a latest-release URL changes with every release, including one published by "
                + "whoever gains control of the repository"),
    ];

    /// <summary>
    /// Requires the raw-content host, so a documentation link that happens to contain <c>/main/</c> does not
    /// match. A commit SHA or a version tag in the same position is fine and deliberately excluded.
    /// </summary>
    [GeneratedRegex(
        @"raw\.githubusercontent\.com/[^/\s]+/[^/\s]+/(main|master|HEAD|develop|trunk)/",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RawContentBranch();

    [GeneratedRegex(
        @"/archive/(refs/heads/|)(main|master|HEAD|develop|trunk)(\.zip|\.tar\.gz|\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArchiveFromBranch();

    /// <summary>
    /// Requires a **fetch verb** on the same line, not just the <c>@latest</c> or <c>:latest</c> selector.
    /// </summary>
    /// <remarks>
    /// The selector alone was measured and was not good enough. On 229 real skills it produced four findings and
    /// one was a skill giving exactly this advice — "Use specific version tags (node:22-alpine, not node:latest)" —
    /// inside a fenced block. The same shape that once broke SF3002: a rule firing on the counter-example someone
    /// cites while agreeing with it.
    ///
    /// Note what did **not** fix it. Reading only code, or only prose, separates neither case: that false positive
    /// sits inside a fence and one of the true positives sits in an inline span in a bullet list. Markdown
    /// structure is the wrong axis. What actually distinguishes them is grammatical — a fetch has a verb. So this
    /// requires one, which keeps both real install commands and drops the advice.
    ///
    /// The cost is stated rather than hidden: a mutable reference invoked through a verb this list does not know
    /// is missed. That is the right direction for a rule nobody asked for, and the list is cheap to extend when a
    /// measurement justifies it.
    /// </remarks>
    [GeneratedRegex(
        @"\b(npm\s+(install|i|add)|npx|pnpm\s+(add|dlx|install)|yarn\s+(add|dlx)|bun\s+(add|x|install)"
            + @"|pip\s+install|pipx\s+install|uv\s+(tool\s+install|pip\s+install)|gem\s+install|go\s+install"
            + @"|cargo\s+install|brew\s+install|apt\s+install|docker\s+(run|pull|build)|podman\s+(run|pull)"
            + @"|FROM)\b[^\n]{0,80}?[\w./-]+[@:]latest\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatestVersion();

    [GeneratedRegex(
        @"/releases/latest\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatestRelease();
}
