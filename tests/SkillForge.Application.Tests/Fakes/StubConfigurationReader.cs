using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// Returns whatever configuration a test wants the skill to have declared.
/// </summary>
/// <remarks>
/// Loader tests are about loading, so the default is "no skillforge.yaml" — the ordinary case. Rule tests that
/// care about declarations build a <see cref="SkillConfiguration"/> directly instead of going through a file.
/// </remarks>
internal sealed class StubConfigurationReader : ISkillConfigurationReader
{
    private readonly SkillConfiguration _configuration;
    private readonly IReadOnlyList<Diagnostic> _diagnostics;

    internal StubConfigurationReader(
        SkillConfiguration? configuration = null,
        IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        _configuration = configuration ?? SkillConfiguration.Default;
        _diagnostics = diagnostics ?? [];
    }

    public Task<OperationResult<SkillConfiguration>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OperationResult<SkillConfiguration>.Success(_configuration, _diagnostics));
}
