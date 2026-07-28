using SkillForge.Domain.Modeling;

namespace SkillForge.Application.Modeling;

/// <summary>
/// Creates a runner for the model the caller named.
/// </summary>
/// <remarks>
/// A factory rather than a registered runner, because the endpoint and model come from the command line or
/// <c>skillforge.yaml</c> at run time — and because a container that built a live HTTP client on startup would make
/// every command that never asks a model pay for one.
/// </remarks>
public interface IModelRunnerFactory
{
    /// <summary>
    /// Creates a runner.
    /// </summary>
    /// <param name="settings">Which model to ask, and where.</param>
    /// <returns>The runner. Nothing is sent until it is asked a question.</returns>
    /// <exception cref="ModelRunnerException">
    /// The settings name an environment variable for the API key and it is not set. Failing here rather than on the
    /// first request means the user is told before any time is spent.
    /// </exception>
    IModelRunner Create(ModelSettings settings);
}
