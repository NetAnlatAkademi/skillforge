namespace SkillForge.Application.Migration;

/// <summary>
/// Where an adapter should look.
/// </summary>
/// <param name="UserDirectory">
/// The user's home directory, passed in rather than read from the environment so a scan can be tested against an
/// in-memory layout and so a caller can inspect somebody else's exported profile.
/// </param>
/// <param name="ProjectDirectory">
/// A project to include project-scoped configuration from, or <see langword="null"/> to look only at user scope.
/// </param>
public sealed record AgentToolScanRequest(string UserDirectory, string? ProjectDirectory);
