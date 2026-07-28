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
}
