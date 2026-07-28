using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Mcp;
using SkillForge.Application.Migration;
using SkillForge.Application.Migration.Adapters;
using SkillForge.Application.Modeling;
using SkillForge.Application.Packaging;
using SkillForge.Application.Providers;
using SkillForge.Application.Skills;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Mcp;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Modeling;
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
        services.AddSingleton<IEvalCaseReader, Infrastructure.Yaml.YamlEvalCaseReader>();
        services.AddSingleton<IArchiveWriter, DeterministicZipArchiveWriter>();
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();

        services.AddSingleton<IAgentProviderRegistry, AgentProviderRegistry>();
        services.AddSingleton<IProviderCompatibilityChecker, ProviderCompatibilityChecker>();

        services.AddSingleton<IUserEnvironment, UserEnvironment>();

        // One reader per format, one adapter per provider — the seam the roadmap asks for, so a provider that moves
        // a file or a protocol that changes touches one class rather than the inspector.
        services.AddSingleton<IMcpConfigurationReader, JsonMcpConfigurationReader>();
        services.AddSingleton<IMcpConfigurationReader, TomlMcpConfigurationReader>();

        services.AddSingleton<SkillInventoryScanner>();
        services.AddSingleton<McpConfigurationScanner>();
        services.AddSingleton<InstructionFileScanner>();
        services.AddSingleton<AgentToolScanner>();

        services.AddSingleton<IAgentToolAdapter, ClaudeCodeToolAdapter>();
        services.AddSingleton<IAgentToolAdapter, CodexToolAdapter>();
        services.AddSingleton<IAgentToolAdapter, CursorToolAdapter>();
        services.AddSingleton<IAgentToolAdapter, GitHubCopilotToolAdapter>();

        // MCP: the declaration checks are free and always run; the protocol adapters only speak when --probe-mcp asks.
        // One adapter per revision, newest first, so the core stays unbound to a protocol version (roadmap §30.6).
        services.AddSingleton<McpDeclarationInspector>();
        services.AddSingleton<IMcpProtocolAdapter>(_ => new Mcp20260728ProtocolAdapter(
            new HttpClient { Timeout = TimeSpan.FromSeconds(20) }));
        services.AddSingleton<McpProber>();

        services.AddSingleton<IMigrationInspector, MigrationInspector>();

        // Registered always, used only when a command is given a model. It opens no connection until
        // it is asked a question, so a run that never mentions a model stays entirely offline.
        services.AddSingleton<IModelRunnerFactory, HttpModelRunnerFactory>();
        services.AddSingleton<SkillCatalogue>();

        services.AddSingleton<ISkillLoader, SkillLoader>();
        services.AddSingleton<ISkillDiscovery, SkillDiscovery>();
        services.AddSingleton<ISkillInitializer, SkillInitializer>();
        services.AddSingleton<ISkillInspector, SkillInspector>();
        services.AddSingleton<ISkillPackager, SkillPackager>();

        services.AddSingleton<IValidationReportRenderer, ConsoleReportRenderer>();
        services.AddSingleton<IValidationReportSerializer, JsonReportSerializer>();
        services.AddSingleton<IValidationReportSerializer, SarifReportSerializer>();

        var fileSystem = new FileSystem();
        foreach (var rule in SkillValidationRules.CreateDefault(fileSystem))
        {
            services.AddSingleton(rule);
        }

        services.AddSingleton<ISkillValidator>(provider =>
            new SkillValidator(provider.GetServices<ISkillValidationRule>()));

        services.AddSingleton<ReportOutput>();
        services.AddSingleton<ValidateCommandRunner>();
        services.AddSingleton<InitCommandRunner>();
        services.AddSingleton<InspectCommandRunner>();
        services.AddSingleton<EvalCommandRunner>();
        services.AddSingleton<DiffCommandRunner>();
        services.AddSingleton<PackCommandRunner>();
        services.AddSingleton<MigrateInspectCommandRunner>();

        // ValidateOnBuild turns a missing or unresolvable registration into a failure here rather than when
        // the user runs a command. It is what makes the composition smoke test meaningful.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
