namespace SkillForge.Cli;

/// <summary>
/// Entry point of the SkillForge CLI.
/// </summary>
/// <remarks>
/// Command definitions, dependency injection bootstrap and exit code mapping are introduced in
/// Phase 3 (CLI Foundation). This placeholder only keeps the executable project buildable.
/// </remarks>
internal static class Program
{
    private const int ExitCodeSuccess = 0;

    internal static int Main()
    {
        Console.WriteLine("SkillForge CLI — bootstrap. No commands are registered yet.");
        return ExitCodeSuccess;
    }
}
