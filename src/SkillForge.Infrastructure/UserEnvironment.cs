using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure;

/// <summary>
/// The real user environment.
/// </summary>
public sealed class UserEnvironment : IUserEnvironment
{
    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Environment.SpecialFolder.UserProfile"/> resolves on every platform SkillForge supports:
    /// <c>%USERPROFILE%</c> on Windows and <c>$HOME</c> elsewhere.
    /// </remarks>
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Environment.GetEnvironmentVariable(name);
    }
}
