using SkillForge.Domain.Identity;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Identity;

/// <summary>
/// Works out what identity an MCP server is reached with, from names and from what the server itself asked for.
/// </summary>
/// <remarks>
/// **It never reads a credential.** Every signal here is the *name* of an environment variable or a header, or
/// something a server's own <c>401</c> stated. That is a real limitation and it is stated in the output rather
/// than papered over: <c>DEPLOY_TOKEN</c> makes a declaration look like it carries a token, and nothing here
/// proves that it does.
///
/// The precedence between signals is the interesting decision. A declaration can carry several at once —
/// a federated token file next to an API key — and the one reported is the most specific machine identity present,
/// because that is the one that decides what the agent can actually do. Reporting the API key instead would name
/// the credential a reviewer least needs to hear about.
/// </remarks>
public static class IdentityInference
{
    /// <summary>Names that mean a platform issued the workload its own identity.</summary>
    private static readonly string[] WorkloadIdentityNames =
    [
        "AWS_WEB_IDENTITY_TOKEN_FILE",
        "AWS_ROLE_ARN",
        "AZURE_FEDERATED_TOKEN_FILE",
        "IDENTITY_ENDPOINT",
        "MSI_ENDPOINT",
        "GCE_METADATA_HOST",
        "KUBERNETES_SERVICE_ACCOUNT_TOKEN",
    ];

    /// <summary>Fragments that mean an account belonging to a system rather than to a person.</summary>
    private static readonly string[] ServiceAccountFragments =
        ["SERVICE_ACCOUNT", "SERVICEACCOUNT", "GOOGLE_APPLICATION_CREDENTIALS", "CLIENT_SECRET"];

    /// <summary>Fragments that mean one token is exchanged for another.</summary>
    private static readonly string[] TokenExchangeFragments =
        ["TOKEN_EXCHANGE", "SUBJECT_TOKEN", "ACTOR_TOKEN", "STS_"];

    /// <summary>Fragments that mean the identity acts for somebody else.</summary>
    private static readonly string[] DelegationFragments =
        ["ON_BEHALF_OF", "OBO_", "DELEGAT", "IMPERSONAT", "ACT_AS", "ACTOR_TOKEN"];

    /// <summary>Fragments that mean a person's own session.</summary>
    private static readonly string[] UserOAuthFragments =
        ["OAUTH", "REFRESH_TOKEN", "ACCESS_TOKEN", "ID_TOKEN"];

    /// <summary>Fragments that mean a static key.</summary>
    private static readonly string[] ApiKeyFragments = ["API_KEY", "APIKEY", "_KEY", "TOKEN", "SECRET", "PASSWORD"];

    /// <summary>Scope fragments that make a delegated identity worth stopping for.</summary>
    private static readonly string[] PrivilegedScopeFragments =
        ["write", "admin", "secret", "delete", "deploy", "manage", "owner", "*"];

    /// <summary>Reads the identity one server declaration implies.</summary>
    /// <param name="server">The declaration.</param>
    /// <param name="probe">What the server said when asked, or <see langword="null"/> when it was not asked.</param>
    /// <returns>The identity, with everything unobserved left unset.</returns>
    public static McpServerIdentity From(McpServerDeclaration server, McpServerProbe? probe = null)
    {
        ArgumentNullException.ThrowIfNull(server);

        var names = new List<string>(server.EnvironmentVariableNames);
        names.AddRange(server.HeaderNamesOrEmpty);

        var evidence = new List<string>();
        evidence.AddRange(server.EnvironmentVariableNames.Select(name => $"env {name} (name only)"));
        evidence.AddRange(server.HeaderNamesOrEmpty.Select(name => $"header {name} (name only)"));

        var challenge = probe?.Authorization;
        if (challenge is not null)
        {
            evidence.Add($"challenge {challenge.Scheme}");
        }

        var isDelegated = names.Any(name => Contains(name, DelegationFragments));
        var type = TypeOf(names, challenge);

        if (type is AgentIdentityType.TokenExchange)
        {
            // An exchanged token is acting for whoever the subject token belonged to, whatever the names say.
            isDelegated = true;
        }

        var identity = new AgentIdentity(
            type,
            IssuerOf(challenge),
            null,
            ScopesOf(challenge),
            isDelegated,
            IsLongLived(type, names));

        var (risk, reason) = Classify(identity);

        return new McpServerIdentity(
            server.Name,
            server.SourcePath,
            identity,
            risk,
            reason,
            [.. evidence.Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// The most specific machine identity the names point at, or what the server's own challenge implies when the
    /// names say nothing.
    /// </summary>
    private static AgentIdentityType TypeOf(IReadOnlyList<string> names, McpAuthorizationChallenge? challenge)
    {
        if (names.Any(name => Contains(name, TokenExchangeFragments)))
        {
            return AgentIdentityType.TokenExchange;
        }

        if (names.Any(name => WorkloadIdentityNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            return AgentIdentityType.WorkloadIdentity;
        }

        if (names.Any(name => Contains(name, ServiceAccountFragments)))
        {
            return AgentIdentityType.ServiceAccount;
        }

        // Below the machine identities and above the credential shapes: when the only thing a declaration says is
        // that it acts for somebody else, that is what it is. A token beside it is how the delegation travels, not
        // what the identity is.
        if (names.Any(name => Contains(name, DelegationFragments)))
        {
            return AgentIdentityType.DelegatedIdentity;
        }

        if (names.Any(name => Contains(name, UserOAuthFragments)))
        {
            return AgentIdentityType.UserOAuth;
        }

        if (names.Any(name => Contains(name, ApiKeyFragments)))
        {
            return AgentIdentityType.ApiKey;
        }

        // A Bearer challenge with nothing else around it is a person being sent to an authorization server. It is
        // the weakest signal here, which is why it is last.
        return challenge is { Scheme.Length: > 0 }
            && challenge.Scheme.Contains("bearer", StringComparison.OrdinalIgnoreCase)
                ? AgentIdentityType.UserOAuth
                : AgentIdentityType.Unknown;
    }

    /// <summary>
    /// Whether the credential outlives the session. An API key and a service-account secret do; a federated token
    /// and an exchanged one are issued for minutes.
    /// </summary>
    /// <remarks>
    /// Read from the names rather than from the type it produced. A declaration can be a delegated identity *and*
    /// hold a static key, and deciding lifetime from the type alone would lose the key the moment the more
    /// specific type won.
    /// </remarks>
    private static bool IsLongLived(AgentIdentityType type, IReadOnlyList<string> names)
    {
        if (type is AgentIdentityType.WorkloadIdentity or AgentIdentityType.TokenExchange)
        {
            // Platform-issued and exchanged tokens are minted for minutes, whatever else is lying beside them.
            return false;
        }

        return names.Any(name =>
            Contains(name, ServiceAccountFragments)
            || Contains(name, ApiKeyFragments)
            || name.Contains("REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The authorization server, when the challenge names its metadata. Never guessed from the URL.
    /// </summary>
    /// <remarks>
    /// The user-info component is dropped rather than carried into the report. This URL comes from a remote
    /// server's own <c>401</c>, so it is somebody else's text: a server that answers with
    /// <c>https://user:secret@auth.example</c> must not have that echoed into a file somebody pastes into a ticket.
    /// </remarks>
    private static string? IssuerOf(McpAuthorizationChallenge? challenge)
    {
        if (challenge?.ResourceMetadataUrl is not { Length: > 0 } url
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.UserInfo.Length == 0
            ? uri.GetLeftPart(UriPartial.Authority)
            : new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }
                .Uri.GetLeftPart(UriPartial.Authority);
    }

    private static IReadOnlyList<string> ScopesOf(McpAuthorizationChallenge? challenge) =>
        challenge?.Scope is { Length: > 0 } scope
            ? [.. scope
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)]
            : [];

    private static (IdentityRisk Risk, string Reason) Classify(AgentIdentity identity)
    {
        var privileged = identity.Scopes
            .Where(scope => Contains(scope, PrivilegedScopeFragments))
            .ToArray();

        if (identity.IsDelegated && privileged.Length > 0)
        {
            return (
                IdentityRisk.High,
                $"A delegated identity carries scopes that change things: {string.Join(", ", privileged)}.");
        }

        if (identity.IsDelegated && identity.IsLongLived)
        {
            return (
                IdentityRisk.High,
                "A delegated identity is backed by a credential that does not expire on its own.");
        }

        if (identity.IsDelegated)
        {
            return (IdentityRisk.Medium, "This identity acts on behalf of another party.");
        }

        if (identity.IsLongLived)
        {
            return (
                IdentityRisk.Medium,
                "The credential does not expire on its own, so revoking it is a deliberate act.");
        }

        return (
            IdentityRisk.Informational,
            identity.Type == AgentIdentityType.Unknown
                ? "Nothing in the declaration says how this server is authorised against."
                : $"Reached with a {Words(identity.Type)}.");
    }

    private static bool Contains(string value, IReadOnlyList<string> fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string Words(AgentIdentityType type) =>
        string.Concat(type.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {char.ToLowerInvariant(character)}" : $"{character}"));
}
