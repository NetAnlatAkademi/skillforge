using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// Instructions inside a skill body that address the agent rather than the reader.
/// </summary>
/// <remarks>
/// **These patterns are narrower than SF3002's were, and the narrowing is the whole point.** SF3002 matched
/// <c>ignore ... other</c> and so fired on <c># Ignore other fields</c>. Every pattern here requires the noun it
/// is actually about — an instruction, a prompt, a rule, a guideline. Ignoring *fields*, *whitespace* or *files*
/// is ordinary technical writing and matches nothing.
///
/// The second defence is the caller: these run against <see cref="MarkdownProse"/>, not raw body text, so a
/// detection pattern quoted in a code block is invisible to them. Either defence alone would have caught one of
/// the two measured false positives; together they catch both.
///
/// Signals, never verdicts (ADR-006). A skill can say "ignore the previous instructions" while meaning the
/// previous instructions in its own document.
/// </remarks>
public static partial class BodyInjectionPatterns
{
    /// <summary>
    /// Prose telling the agent to set aside instructions it was given. This is the shape prompt injection takes
    /// when it arrives inside a skill body rather than inside user input.
    /// </summary>
    public static IReadOnlyList<RiskPattern> InstructionOverride { get; } =
    [
        new(
            "an instruction to set aside earlier instructions",
            IgnoreInstructions(),
            "a skill body tells an agent how to do the skill's work; telling it which of its own instructions "
                + "to drop is a different kind of request, and it is the shape prompt injection takes"),
        new(
            "a claim to override the agent's instructions",
            OverrideInstructions(),
            "a skill that claims authority over its surroundings is asking for more than a place in the "
                + "agent's toolbox"),
    ];

    /// <summary>
    /// Prose telling the agent to keep something from the person it is working for. A skill has no legitimate
    /// need for this: whatever it does, the user is entitled to know it happened.
    /// </summary>
    public static IReadOnlyList<RiskPattern> Concealment { get; } =
    [
        new(
            "an instruction not to tell the user something",
            DoNotTellTheUser(),
            "the person running the agent is entitled to know what it did on their behalf, so text asking for "
                + "the opposite is worth a human's attention"),
        new(
            "an instruction to act without informing the user",
            WithoutInformingTheUser(),
            "acting first and explaining later is a choice for the user to make, not for a skill to make on "
                + "their behalf"),
        new(
            "an instruction to act silently or secretly",
            SilentAction(),
            "a step described as silent or secret is being hidden from someone, and the only candidate is the "
                + "user"),
    ];

    /// <summary>
    /// Requires the noun, unlike SF3002's version. <c>ignore ... other</c> alone matched
    /// <c># Ignore other fields</c> on real data; <c>ignore ... other instructions</c> does not.
    /// </summary>
    [GeneratedRegex(
        @"\b(ignore|disregard|forget)\b[^.]{0,40}\b(previous|prior|earlier|preceding|above|other|any|all|system)\b"
            + @"[^.]{0,25}\b(instruction|prompt|guideline|direction|rule)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IgnoreInstructions();

    [GeneratedRegex(
        @"\boverrid(e|es|ing)\b[^.]{0,40}\b(instruction|prompt|guideline|direction|rule|behaviour|behavior)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OverrideInstructions();

    /// <summary>
    /// Requires a verb of *disclosure*, and then refuses the infinitive that follows an instruction.
    /// </summary>
    /// <remarks>
    /// English puts concealment and instruction in the same words and separates them by what comes next:
    /// "do not tell the user **that** this ran" hides something, "do not tell the user **to** run it twice" is
    /// advice about what to say. The trailing <c>(?!\s+to\b)</c> is that distinction, and it is the difference
    /// between a rule worth shipping and one that fires on ordinary skill instructions.
    /// </remarks>
    [GeneratedRegex(
        @"\b(do not|don't|never|avoid)\s+(\w+\s+){0,2}?(tell|telling|inform|informing|mention|mentioning|reveal"
            + @"|revealing|disclose|disclosing|show|showing|report|reporting)\b[^.]{0,20}\b(the\s+)?"
            + @"(user|human|operator|caller)\b(?!\s+to\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DoNotTellTheUser();

    [GeneratedRegex(
        @"\bwithout\s+(\w+\s+){0,2}?(telling|informing|notifying|asking|alerting|warning|the knowledge of)\b"
            + @"[^.]{0,20}\b(the\s+)?(user|human|operator|caller)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WithoutInformingTheUser();

    /// <summary>
    /// "Silently" and "quietly" are common in ordinary technical writing — a cache that fails quietly, a check
    /// that silently passes — so this needs a verb that *acts on something outside the skill* to match.
    /// </summary>
    [GeneratedRegex(
        @"\b(silently|secretly|covertly|without a trace)\b[^.]{0,20}\b(send|sends|sending|upload|uploads"
            + @"|uploading|post|posts|posting|transmit|transmits|transmitting|exfiltrate|copy|copies|copying"
            + @"|delete|deletes|deleting|modify|modifies|modifying|install|installs|installing)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SilentAction();
}
