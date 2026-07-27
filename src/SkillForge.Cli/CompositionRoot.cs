using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Packaging;
using SkillForge.Application.Skills;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Yaml;
using SkillForge.Reporting;

namespace SkillForge.Cli;

/// <summary>
/// The one place where interfaces are bound to implementations.
/// </summary>
/// <remarks>
/// Every dependency is registered here so no other class has to know what implements what. The rule set comes
/// from <see cref="SkillValidationRules.CreateDefault"/> — an explicit list, not assembly scanning, so a rule
/// cannot go missing without somebody removing a line.
/// </remarks>
internal static class CompositionRoot
{
    /// <summary>Builds the service provider the CLI runs on.</summary>
    /// <returns>A configured provider. The caller owns its lifetime.</returns>
    internal static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IFrontmatterParser, YamlFrontmatterParser>();
        services.AddSingleton<ISkillConfigurationReader, YamlSkillConfigurationReader>();
        services.AddSingleton<IArchiveWriter, DeterministicZipArchiveWriter>();
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();

        services.AddSingleton<ISkillLoader, SkillLoader>();
        services.AddSingleton<ISkillDiscovery, SkillDiscovery>();
        services.AddSingleton<ISkillInitializer, SkillInitializer>();
        services.AddSingleton<ISkillInspector, SkillInspector>();
        services.AddSingleton<ISkillPackager, SkillPackager>();

        services.AddSingleton<IValidationReportRenderer, ConsoleReportRenderer>();
        services.AddSingleton<IValidationReportSerializer, JsonReportSerializer>();
        services.AddSingleton<IValidationReportSerializer, SarifReportSerializer>();

        foreach (var rule in SkillValidationRules.CreateDefault())
        {
            services.AddSingleton(rule);
        }

        services.AddSingleton<ISkillValidator>(provider =>
            new SkillValidator(provider.GetServices<ISkillValidationRule>()));

        services.AddSingleton<ReportOutput>();
        services.AddSingleton<ValidateCommandRunner>();
        services.AddSingleton<InitCommandRunner>();
        services.AddSingleton<InspectCommandRunner>();
        services.AddSingleton<DiffCommandRunner>();
        services.AddSingleton<PackCommandRunner>();

        // ValidateOnBuild turns a missing or unresolvable registration into a failure here rather than when
        // the user runs a command. It is what makes the composition smoke test meaningful.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
