namespace SkillForge.Application.Validation;

/// <summary>
/// One line of a skill body's prose, with the line number it came from.
/// </summary>
/// <param name="Text">
/// The line with its code spans removed. Not the original text — a rule matching against this reports a
/// position, never a quotation.
/// </param>
/// <param name="Line">One-based line number within the file the body belongs to.</param>
public sealed record ProseLine(string Text, int Line);
