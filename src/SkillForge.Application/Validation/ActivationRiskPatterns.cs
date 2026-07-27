using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// Activation text that claims too much, and activation text that pushes rather than describes.
/// </summary>
/// <remarks>
/// **How these were validated matters, because it is not the same way the other rules were.** Measured across 203
/// real skill descriptions, every one of these patterns fires on at most one skill. For a quality rule that would
/// be a reason not to ship it; for these it is the goal. A skill that tells an agent to ignore its other
/// instructions is what an attacker writes, and attackers are not in a sample of benign skills — so measuring
/// benign input proves the absence of false positives, not the absence of value. The value is demonstrated with
/// deliberately crafted positives in the tests instead.
///
/// Signals, never verdicts (ADR-006). "Always" in a description is usually just enthusiasm.
/// </remarks>
public static partial class ActivationRiskPatterns
{
    /// <summary>
    /// Claims of universal applicability. An activation scope of "everything" is no scope at all: an agent choosing
    /// between skills has nothing to choose on.
    /// </summary>
    public static IReadOnlyList<RiskPattern> TooBroad { get; } =
    [
        new(
            "\"always\"",
            Always(),
            "a skill that always applies gives an agent nothing to decide on, so it competes with every other "
                + "skill instead of being chosen when it fits"),
        new(
            "every or all requests",
            EveryRequest(),
            "the same problem stated more explicitly — describe the situation the skill is for instead"),
        new(
            "at all times",
            AtAllTimes(),
            "an activation scope of \"whenever\" cannot be matched against a task"),
    ];

    /// <summary>
    /// Text that tries to win activation rather than describe it. This is the shape of activation manipulation:
    /// instructions aimed at the agent's decision-making rather than at the reader's understanding.
    /// </summary>
    public static IReadOnlyList<RiskPattern> Manipulation { get; } =
    [
        new(
            "an instruction to ignore other instructions",
            IgnoreInstructions(),
            "a skill describing when it applies has no reason to tell an agent what to disregard; this is the "
                + "shape prompt injection takes when it arrives inside a skill"),
        new(
            "an instruction not to use other skills",
            PreferOverOthers(),
            "which skill fits a task is the agent's decision, and text that argues with that decision is not "
                + "describing a capability"),
        new(
            "a claim of overriding other behaviour",
            Override(),
            "a skill that claims to override its surroundings is asking for more than activation"),
        new(
            "a demand to act before anything else",
            BeforeAnything(),
            "ordering an agent to run something before every response is activation pressure, not a description"),
    ];

    [GeneratedRegex(@"\balways\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Always();

    [GeneratedRegex(
        @"\b(every|all|any)\s+(request|task|prompt|conversation|message|interaction|question|response)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EveryRequest();

    [GeneratedRegex(@"\b(at all times|any ?time|regardless of (the )?(context|task|request))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AtAllTimes();

    [GeneratedRegex(@"\b(ignore|disregard|forget)\b[^.]{0,40}\b(instruction|previous|prior|other|above|system)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IgnoreInstructions();

    /// <remarks>
    /// "instead of" and "rather than" were here and are gone. Measured on 229 real skills they produced eight
    /// findings and every one was ordinary English — "say so instead of hiding behind tooling", "reuse it instead of
    /// re-deriving". A phrase that common cannot carry a security signal.
    /// </remarks>
    [GeneratedRegex(@"\b(do not|don't|never)\s+use\b[^.]{0,25}\b(other|another|any other)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PreferOverOthers();

    [GeneratedRegex(@"\boverrid(e|es|ing)\b[^.]{0,40}\b(instruction|skill|rule|behaviour|behavior|prompt|system)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Override();

    [GeneratedRegex(@"\bbefore\s+(any|every|each)\s+(response|reply|answer|action|tool)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BeforeAnything();
}
