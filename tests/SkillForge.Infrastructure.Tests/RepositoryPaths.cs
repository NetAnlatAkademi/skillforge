namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// Locates repository-relative fixtures from the test output directory.
/// </summary>
/// <remarks>
/// Walking up to the solution file keeps tests independent of build configuration and of how deep the
/// output directory happens to be, which differs between local runs and CI.
/// </remarks>
internal static class RepositoryPaths
{
    private const string SolutionFileName = "SkillForge.slnx";

    /// <summary>Absolute path of the repository root.</summary>
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Absolute path of the <c>samples</c> directory.</summary>
    internal static string SamplesDirectory { get; } = Path.Combine(RepositoryRoot, "samples");

    /// <summary>Absolute path of a named sample skill.</summary>
    /// <param name="sampleName">Directory name of the sample, for example <c>valid-skill</c>.</param>
    /// <returns>The sample's absolute path.</returns>
    internal static string Sample(string sampleName) => Path.Combine(SamplesDirectory, sampleName);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find '{SolutionFileName}' above '{AppContext.BaseDirectory}'.");
    }
}
