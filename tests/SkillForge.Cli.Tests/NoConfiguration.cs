using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Tests;

/// <summary>
/// A skill with no <c>skillforge.yaml</c>, which is the ordinary case and the right default for tests that are
/// not about configuration.
/// </summary>
internal sealed class NoConfiguration : ISkillConfigurationReader
{
    public Task<OperationResult<SkillConfiguration>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<SkillConfiguration>.Success(SkillConfiguration.Default));
}
