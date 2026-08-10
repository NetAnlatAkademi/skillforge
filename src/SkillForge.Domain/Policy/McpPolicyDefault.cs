namespace SkillForge.Domain.Policy;

/// <summary>
/// What an MCP policy does about a server no rule names.
/// </summary>
/// <remarks>
/// <see cref="NotDeclared"/> exists because "the policy did not say" is a different fact from either decision, and
/// collapsing it into one of them would make the tool decide something the organisation did not. It is reported
/// (<c>SF8104</c>) rather than resolved.
/// </remarks>
public enum McpPolicyDefault
{
    /// <summary>The policy states no default, so what happens to an unnamed server is not written down.</summary>
    NotDeclared,

    /// <summary>A server no rule names is permitted.</summary>
    Allow,

    /// <summary>A server no rule names is refused. The only fail-closed answer.</summary>
    Deny,
}
