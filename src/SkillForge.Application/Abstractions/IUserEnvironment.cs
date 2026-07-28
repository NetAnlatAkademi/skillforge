namespace SkillForge.Application.Abstractions;

/// <summary>
/// The user-specific locations SkillForge needs, behind an abstraction so nothing in the Application layer reads
/// the environment directly.
/// </summary>
/// <remarks>
/// It exists for <c>migrate inspect</c>, which is the first command whose input is "wherever this person's tools
/// keep their configuration" rather than a path they typed. Making it a dependency keeps that command testable
/// against an in-memory home directory and lets a caller point it somewhere else.
/// </remarks>
public interface IUserEnvironment
{
    /// <summary>Gets the current user's home directory.</summary>
    string HomeDirectory { get; }

    /// <summary>
    /// Reads an environment variable.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <returns>Its value, or <see langword="null"/> when it is not set.</returns>
    /// <remarks>
    /// Used for one thing: the API key whose variable name a model configuration gives. The value is never stored on a
    /// model, written to a report or logged — reading it through an abstraction is also what lets the tests prove that
    /// without setting a real key.
    /// </remarks>
    string? GetEnvironmentVariable(string name);
}
