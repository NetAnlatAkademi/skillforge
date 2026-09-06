namespace SkillForge.Domain.Identity;

/// <summary>
/// The kind of identity an agent reaches a server with.
/// </summary>
/// <remarks>
/// The distinction that matters is not the technology, it is who is answerable for what the agent does. A user's
/// OAuth token means a person authorised this and their permissions bound it. A workload identity means the
/// machine did, with whatever the workload was granted — which is usually more, and is not revoked when the person
/// leaves.
/// </remarks>
public enum AgentIdentityType
{
    /// <summary>Nothing observed says how the server is authorised against.</summary>
    Unknown,

    /// <summary>A person's own OAuth session.</summary>
    UserOAuth,

    /// <summary>An account that belongs to a system rather than to a person.</summary>
    ServiceAccount,

    /// <summary>A federated identity the platform issues to the workload itself.</summary>
    WorkloadIdentity,

    /// <summary>An identity acting on behalf of another party.</summary>
    DelegatedIdentity,

    /// <summary>One token exchanged for another, usually to cross a trust boundary.</summary>
    TokenExchange,

    /// <summary>A static key, presented as-is.</summary>
    ApiKey,
}

/// <summary>
/// How much an identity is worth stopping for.
/// </summary>
public enum IdentityRisk
{
    /// <summary>Nothing observed raises a question.</summary>
    Informational,

    /// <summary>Something worth knowing: a credential that does not expire, or an identity acting for another.</summary>
    Medium,

    /// <summary>Both at once, or a delegated identity carrying scopes that write or read secrets.</summary>
    High,
}

/// <summary>
/// What could be established about the identity an MCP server is reached with.
/// </summary>
/// <remarks>
/// **No value that could be a credential is ever read into this type.** Everything here comes from names — of
/// environment variables, of headers — and from what a server's own <c>401</c> said about the authorization it
/// wants. A token is never read, never stored and never printed.
///
/// It is inference from names, and it says so wherever it is reported. <c>DEPLOY_TOKEN</c> makes a declaration
/// look like it carries a token; nothing here proves it does.
/// </remarks>
/// <param name="Type">The kind of identity the evidence points at.</param>
/// <param name="Issuer">Who issues it, when a server's challenge names its authorization metadata.</param>
/// <param name="Subject">
/// Who it identifies. Always <see langword="null"/> today: a subject is inside the token, and SkillForge does not
/// read tokens. Present in the model because a diff of subjects is what identity drift looks like, and the field
/// has to exist before anything can fill it honestly.
/// </param>
/// <param name="Scopes">Scopes the server asked for, ordered.</param>
/// <param name="IsDelegated">Whether the evidence says this identity acts on behalf of another party.</param>
/// <param name="IsLongLived">Whether the evidence points at a credential that does not expire on its own.</param>
public sealed record AgentIdentity(
    AgentIdentityType Type,
    string? Issuer,
    string? Subject,
    IReadOnlyList<string> Scopes,
    bool IsDelegated,
    bool IsLongLived)
{
    /// <summary>An identity nothing observed says anything about.</summary>
    public static AgentIdentity Unknown { get; } = new(AgentIdentityType.Unknown, null, null, [], false, false);
}
