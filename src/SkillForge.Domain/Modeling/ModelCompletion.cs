namespace SkillForge.Domain.Modeling;

/// <summary>
/// What a model answered.
/// </summary>
/// <param name="Text">The reply, trimmed. Empty when the model returned no content.</param>
/// <param name="PromptTokens">Tokens the endpoint reported for the request, or 0 when it reported none.</param>
/// <param name="CompletionTokens">Tokens the endpoint reported for the reply, or 0 when it reported none.</param>
/// <remarks>
/// Token counts are carried because a command that spends somebody's money should say how much it spent. Not every
/// OpenAI-compatible endpoint reports usage, so zero means "not reported" rather than "free".
/// </remarks>
public sealed record ModelCompletion(string Text, int PromptTokens, int CompletionTokens);
