namespace SkillForge.Application.Migration;

/// <summary>
/// Reads one provider's installation: where it keeps skills, how it declares MCP servers, which instruction
/// files it reads.
/// </summary>
/// <remarks>
/// One adapter per provider, and every path a provider uses is stated in that adapter and nowhere else. This is
/// what the roadmap means by keeping the core free of provider and protocol specifics: a provider that moves its
/// configuration file changes one class, and the MCP protocol adapters will sit beside these rather than inside
/// the inspector.
///
/// An adapter never fails because a provider is absent. "Not installed" is one of the answers the command exists
/// to give.
/// </remarks>
public interface IAgentToolAdapter
{
    /// <summary>Gets the provider identifier this adapter speaks for, matching the provider registry.</summary>
    string ProviderId { get; }

    /// <summary>
    /// Looks for this provider and reads what it declares.
    /// </summary>
    /// <param name="request">Where to look.</param>
    /// <param name="cancellationToken">Token used to cancel the scan.</param>
    /// <returns>What was found, which may be nothing at all.</returns>
    Task<AgentToolScan> ScanAsync(AgentToolScanRequest request, CancellationToken cancellationToken = default);
}
