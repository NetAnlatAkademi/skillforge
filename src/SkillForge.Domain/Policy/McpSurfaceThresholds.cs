namespace SkillForge.Domain.Policy;

/// <summary>
/// How many tools an organisation is willing to have exposed to an agent at once.
/// </summary>
/// <remarks>
/// A threshold rather than a rule, and configurable rather than fixed, because the right number is not knowable
/// from here: it depends on the model's context, on how the tools are named and on what the team is doing. What is
/// knowable is that the number matters, and that somebody should have picked it deliberately.
///
/// The defaults are a starting point, not a recommendation with evidence behind it. They are stated in the output
/// as defaults so nobody mistakes them for a measurement.
/// </remarks>
/// <param name="WarningToolCount">Tool count at which the surface is worth reporting.</param>
/// <param name="HighToolCount">Tool count at which it is worth stopping for.</param>
public sealed record McpSurfaceThresholds(int WarningToolCount, int HighToolCount)
{
    /// <summary>Gets the thresholds used when the policy does not say.</summary>
    public static McpSurfaceThresholds Default { get; } = new(50, 100);
}
