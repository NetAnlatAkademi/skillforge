namespace SkillForge.Domain.Modeling;

/// <summary>
/// Which model to ask, and where it lives.
/// </summary>
/// <remarks>
/// SkillForge sends nothing anywhere unless these are supplied and the caller asks for a model explicitly. There is
/// no default endpoint and no default model: a tool that quietly starts making network calls because a file existed
/// would have broken the promise the rest of the product is built on.
///
/// <see cref="ApiKeyEnvironmentVariable"/> is the **name** of an environment variable, never a key. A key belongs in
/// neither a repository nor a report, and the only way to be sure it is in neither is to have no field for it.
/// </remarks>
/// <param name="Endpoint">
/// Base URL of an OpenAI-compatible API — <c>http://localhost:11434/v1</c> for Ollama,
/// <c>https://api.openai.com/v1</c> for OpenAI. The compatible shape is what lets one adapter reach both a local
/// runner and a hosted one.
/// </param>
/// <param name="Name">The model identifier the endpoint expects, for example <c>qwen3:8b</c> or <c>gpt-5</c>.</param>
/// <param name="ApiKeyEnvironmentVariable">
/// Name of the environment variable holding the API key, or <see langword="null"/> for an endpoint that needs none —
/// which is the ordinary case for a local runner.
/// </param>
public sealed record ModelSettings(string Endpoint, string Name, string? ApiKeyEnvironmentVariable)
{
    /// <summary>Gets the model's identity for a report, which never includes a key.</summary>
    public ModelIdentity Identity => new(Endpoint, Name);
}
