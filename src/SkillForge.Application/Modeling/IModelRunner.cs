using SkillForge.Domain.Modeling;

namespace SkillForge.Application.Modeling;

/// <summary>
/// Asks a model a question.
/// </summary>
/// <remarks>
/// The one place SkillForge talks to something outside the machine, behind an interface for the usual reason and for
/// one more: every test in this repository must be able to run with no network and no model, so nothing may depend on
/// a real endpoint being reachable.
///
/// One adapter per API shape, chosen the same way the MCP readers are: OpenAI-compatible first, because Ollama,
/// LM Studio, llama.cpp, vLLM, OpenRouter, Azure OpenAI and OpenAI itself all speak it, so one adapter reaches both
/// a local model and a hosted one.
/// </remarks>
public interface IModelRunner
{
    /// <summary>Gets the model this runner will ask, for the report's provenance.</summary>
    ModelIdentity Identity { get; }

    /// <summary>
    /// Puts one question to the model.
    /// </summary>
    /// <param name="prompt">What to ask.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The reply.</returns>
    /// <exception cref="ModelRunnerException">
    /// The endpoint could not be reached, refused the request, or answered in a shape this adapter does not
    /// understand. Thrown rather than returned as a result: an unreachable model is not a finding about a skill, and
    /// silently reporting "did not fire" for a failed request would be a lie about the skill.
    /// </exception>
    Task<ModelCompletion> CompleteAsync(ModelPrompt prompt, CancellationToken cancellationToken = default);
}
