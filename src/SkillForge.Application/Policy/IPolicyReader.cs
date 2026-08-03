using SkillForge.Domain;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Policy;

/// <summary>
/// Reads an organisation's policy file.
/// </summary>
public interface IPolicyReader
{
    /// <summary>The path a policy lives at when the caller does not name one.</summary>
    const string DefaultPath = ".skillforge/policy.yaml";

    /// <summary>Reads a policy.</summary>
    /// <param name="path">Path of the policy file.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The policy, or a failure carrying <c>SF9001</c> when the file exists and could not be read. A file that is
    /// absent is a failure too, and the caller decides what that means: asking for a policy that is not there is
    /// different from not asking for one.
    /// </returns>
    Task<OperationResult<PolicyDocument>> ReadAsync(string path, CancellationToken cancellationToken = default);
}
