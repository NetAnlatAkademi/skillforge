namespace SkillForge.Application.Modeling;

/// <summary>
/// The model could not be asked, or answered unusably.
/// </summary>
/// <remarks>
/// A distinct exception so the command can tell the user their endpoint is wrong instead of reporting that a skill
/// failed to activate. Treating an unreachable model as a negative result would be the worst possible failure mode
/// here: it looks like evidence about the skill and is evidence about the network.
/// </remarks>
public sealed class ModelRunnerException : Exception
{
    /// <summary>Initialises the exception.</summary>
    /// <param name="message">What went wrong, in terms the person running the CLI can act on.</param>
    public ModelRunnerException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying failure.</param>
    public ModelRunnerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initialises the exception with no message, required by analyzers.</summary>
    public ModelRunnerException()
    {
    }
}
