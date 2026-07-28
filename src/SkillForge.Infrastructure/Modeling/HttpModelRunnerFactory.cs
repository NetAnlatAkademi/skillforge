using System.Net.Http.Headers;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Modeling;
using SkillForge.Domain.Modeling;

namespace SkillForge.Infrastructure.Modeling;

/// <summary>
/// Builds an <see cref="OpenAiCompatibleModelRunner"/> with a client pointed at the caller's endpoint.
/// </summary>
/// <remarks>
/// The API key is read here, from the environment variable the settings name, and handed to the client as a header. It
/// exists in one place, for the lifetime of one command, and appears in no model and no report.
///
/// A named-but-unset variable fails immediately. The alternative — sending an unauthenticated request and surfacing
/// the endpoint's 401 — tells the user their endpoint rejected them when the truth is their shell has no key in it.
/// </remarks>
public sealed class HttpModelRunnerFactory : IModelRunnerFactory, IDisposable
{
    /// <summary>
    /// Long enough for a local model on a cold start, short enough that a wrong host does not hang a build.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);

    private readonly IUserEnvironment _environment;
    private readonly List<HttpClient> _clients = [];

    /// <summary>Initialises the factory.</summary>
    /// <param name="environment">Used to read the named API key variable.</param>
    public HttpModelRunnerFactory(IUserEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <inheritdoc />
    public IModelRunner Create(ModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var client = new HttpClient
        {
            // The trailing slash matters: without it, a relative "chat/completions" replaces the last path segment,
            // so an endpoint ending in /v1 would be asked for /chat/completions and answer 404.
            BaseAddress = new Uri(settings.Endpoint.EndsWith('/') ? settings.Endpoint : settings.Endpoint + '/'),
            Timeout = RequestTimeout,
        };

        if (settings.ApiKeyEnvironmentVariable is { Length: > 0 } variable)
        {
            var key = _environment.GetEnvironmentVariable(variable);

            if (key is null or { Length: 0 })
            {
                client.Dispose();

                throw new ModelRunnerException(
                    $"The environment variable '{variable}' is not set, so there is no API key to send to "
                    + $"'{settings.Endpoint}'. Set it, or drop the key setting for an endpoint that needs none.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        _clients.Add(client);

        return new OpenAiCompatibleModelRunner(client, settings);
    }

    /// <summary>Disposes every client this factory created.</summary>
    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
    }
}
