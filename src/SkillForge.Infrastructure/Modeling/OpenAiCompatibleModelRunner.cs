using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Modeling;
using SkillForge.Domain.Modeling;

namespace SkillForge.Infrastructure.Modeling;

/// <summary>
/// Talks to any endpoint that implements OpenAI's <c>/chat/completions</c>.
/// </summary>
/// <remarks>
/// One adapter, many runners: Ollama, LM Studio, llama.cpp's server, vLLM, OpenRouter, Azure OpenAI and OpenAI itself
/// all accept this request shape. That is the whole reason this shape was chosen over any provider's native API — the
/// operator picks the model, local or hosted, and SkillForge does not have to bless a vendor to make it work.
///
/// <c>temperature: 0</c> is sent because the point is to sample a decision, not to see the model be creative. It
/// reduces variation; it does not remove it, which is why the prober asks more than once.
///
/// The API key is read from the environment variable the settings **name**. It is never logged, never put in a report,
/// and there is no field on any model that could hold it.
/// </remarks>
public sealed class OpenAiCompatibleModelRunner : IModelRunner
{
    private const string CompletionsPath = "chat/completions";

    private readonly HttpClient _client;
    private readonly ModelSettings _settings;

    /// <summary>Initialises the runner.</summary>
    /// <param name="client">
    /// The client to send with. Its <c>BaseAddress</c> and authorisation header are configured by the composition
    /// root, so this class holds no key and no transport policy.
    /// </param>
    /// <param name="settings">Which model to ask.</param>
    public OpenAiCompatibleModelRunner(HttpClient client, ModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        _client = client;
        _settings = settings;
    }

    /// <inheritdoc />
    public ModelIdentity Identity => _settings.Identity;

    /// <inheritdoc />
    public async Task<ModelCompletion> CompleteAsync(
        ModelPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var request = new JsonObject
        {
            ["model"] = _settings.Name,
            ["temperature"] = 0,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = prompt.System },
                new JsonObject { ["role"] = "user", ["content"] = prompt.User }),
        };

        // A string body rather than PostAsJsonAsync, so the request carries Content-Length instead of being sent
        // chunked. Every real endpoint accepts chunked, but plenty of small local servers and gateways do not, and
        // they fail by closing the socket — which reaches the user as an unexplained "could not send".
        using var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _client
                .PostAsync(CompletionsPath, content, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // The commonest failure by far: nothing listening, or a wrong host. Say which endpoint, because the
            // person has usually just typed it.
            throw new ModelRunnerException(
                $"Could not reach the model endpoint '{_settings.Endpoint}': {Innermost(exception).Message}",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelRunnerException(
                $"The model endpoint '{_settings.Endpoint}' did not answer in time.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                throw new ModelRunnerException(
                    $"The model endpoint '{_settings.Endpoint}' refused the request "
                    + $"({(int)response.StatusCode} {response.ReasonPhrase}): {Shorten(body)}");
            }

            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return Read(payload);
        }
    }

    private ModelCompletion Read(string payload)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new ModelRunnerException(
                $"The model endpoint '{_settings.Endpoint}' answered with something that is not JSON.",
                exception);
        }

        var text = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

        if (text is null)
        {
            // An endpoint that speaks a different dialect fails here rather than being reported as a model that
            // declined to choose a skill — those two must never look the same.
            throw new ModelRunnerException(
                $"The model endpoint '{_settings.Endpoint}' answered in a shape this adapter does not understand: "
                + "no choices[0].message.content. It may not be OpenAI-compatible.");
        }

        var usage = root?["usage"];

        return new ModelCompletion(
            text.Trim(),
            Count(usage, "prompt_tokens"),
            Count(usage, "completion_tokens"));
    }

    /// <summary>Usage is optional in practice, so a missing count is zero rather than an error.</summary>
    private static int Count(JsonNode? usage, string property) =>
        usage?[property] is { } value && value.AsValue().TryGetValue<int>(out var count) ? count : 0;

    /// <summary>
    /// The exception at the bottom of the chain, because that is the one with the useful sentence in it: an
    /// HttpRequestException says "an error occurred while sending the request" and its inner socket exception says
    /// "no connection could be made because the target machine actively refused it".
    /// </summary>
    private static Exception Innermost(Exception exception)
    {
        var current = exception;

        while (current.InnerException is { } inner)
        {
            current = inner;
        }

        return current;
    }

    private static string Shorten(string body) =>
        body.Length <= 300 ? body : string.Concat(body.AsSpan(0, 300), "…");
}
