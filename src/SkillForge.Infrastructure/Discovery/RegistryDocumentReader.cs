using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;

namespace SkillForge.Infrastructure.Discovery;

/// <summary>
/// Fetches and parses a registry document under every bound a discovery request carries.
/// </summary>
/// <remarks>
/// Shared by the discovery adapters because the bounds are the part that must not differ between them. Two
/// adapters with their own idea of how large a response may be would mean one of them is the weak one, and the
/// weak one is the one an attacker picks. Everything registry-specific — which container the resources live in,
/// what a field is called — stays in each adapter, where it belongs.
///
/// **All of these bounds exist because the other end is not ours.** A registry is a URL somebody typed on a
/// command line, and a URL somebody typed can answer slowly, answer enormously, redirect back to itself, or answer
/// with JSON nested a thousand deep. None of those has to be clever to hang a CI job.
/// </remarks>
internal sealed class RegistryDocumentReader
{
    private readonly HttpClient _client;

    /// <summary>Initialises the reader.</summary>
    /// <param name="client">
    /// The client to fetch with. Its handler owns the redirect bound, because redirect following happens below
    /// this class; every other bound travels with the request.
    /// </param>
    internal RegistryDocumentReader(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Fetches a document and parses it.
    /// </summary>
    /// <param name="uri">What to fetch.</param>
    /// <param name="limits">The bounds to fetch and parse under.</param>
    /// <param name="cancellationToken">The caller's token. Cancellation by it is never swallowed.</param>
    /// <returns>
    /// The parsed document, or the diagnostic explaining why there is none. Never both, and never an exception:
    /// a registry that will not answer is a fact about that registry rather than a reason to abandon a run.
    /// </returns>
    internal async Task<(JsonNode? Document, Diagnostic? Diagnostic)> ReadAsync(
        Uri uri,
        RemoteDiscoveryLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(limits);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limits.Timeout);

        var body = await FetchAsync(uri, limits, timeout.Token, cancellationToken).ConfigureAwait(false);

        if (body.Diagnostic is { } failure)
        {
            return (null, failure);
        }

        try
        {
            return (
                JsonNode.Parse(
                    body.Body!,
                    nodeOptions: null,
                    documentOptions: new JsonDocumentOptions { MaxDepth = limits.MaxJsonDepth }),
                null);
        }
        catch (JsonException exception)
        {
            return (null, Unusable(uri, $"the response could not be read as JSON: {exception.Message}"));
        }
    }

    /// <summary>
    /// A diagnostic saying a registry could not be searched.
    /// </summary>
    /// <param name="uri">The registry.</param>
    /// <param name="reason">Why, in the underlying failure's own words.</param>
    /// <returns>The diagnostic.</returns>
    internal static Diagnostic Unusable(Uri uri, string reason) =>
        Diagnostic.Warning(
            DiagnosticCodes.DiscoveryResponseNotUsable,
            $"'{uri}' could not be searched: {reason}.",
            uri.ToString(),
            suggestion: "Discovery reads a registry and nothing else. Check the URL, and note that SkillForge "
                + "sends the URL as given rather than inventing a search parameter for it.");

    /// <summary>
    /// A diagnostic saying more was listed than was read.
    /// </summary>
    /// <param name="uri">The registry.</param>
    /// <param name="listed">How many resources it listed.</param>
    /// <param name="read">How many were read.</param>
    /// <returns>The diagnostic.</returns>
    internal static Diagnostic Truncated(Uri uri, int listed, int read) =>
        Diagnostic.Info(
            DiagnosticCodes.DiscoveryResponseNotUsable,
            $"'{uri}' listed {listed} resources, over the {read} this run reads, so the result is a partial view "
                + "of what the registry holds.",
            uri.ToString(),
            suggestion: "Narrow the query. A search that returns everything is not a search, and a registry "
                + "cannot be verified in bulk anyway — verification asks each server itself.");

    /// <summary>
    /// Fetches the body under the size bound.
    /// </summary>
    /// <remarks>
    /// The body is read through a bounded copy rather than <c>ReadAsStringAsync</c>. A declared
    /// <c>Content-Length</c> is checked first because refusing before reading is cheaper, but it is not trusted:
    /// a response may be chunked, or may simply lie, so the copy stops at the limit either way.
    ///
    /// An oversized body is **refused, not truncated**. Half a JSON document parses into something other than what
    /// was sent, and acting on the difference is worse than reporting the size.
    /// </remarks>
    private async Task<(string? Body, Diagnostic? Diagnostic)> FetchAsync(
        Uri uri,
        RemoteDiscoveryLimits limits,
        CancellationToken effective,
        CancellationToken caller)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Accept", "application/ld+json, application/json");

            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effective)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return (null, Unusable(
                    uri,
                    $"the registry answered {(int)response.StatusCode} {response.ReasonPhrase}"));
            }

            if (response.Content.Headers.ContentLength is { } declared && declared > limits.MaxResponseBytes)
            {
                return (null, Unusable(
                    uri,
                    $"the registry declared a {declared}-byte response, over the {limits.MaxResponseBytes}-byte "
                        + "limit"));
            }

            var body = await BoundedBodyAsync(response, limits.MaxResponseBytes, effective).ConfigureAwait(false);

            return body is null
                ? (null, Unusable(uri, $"the registry's response exceeded the {limits.MaxResponseBytes}-byte limit"))
                : (body, null);
        }
        catch (HttpRequestException exception)
        {
            return (null, Unusable(uri, Innermost(exception).Message));
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            // The request's own timeout, not the user's Ctrl+C: a cancelled run must still cancel.
            return (null, Unusable(
                uri,
                $"the registry did not answer within {limits.Timeout.TotalSeconds:0.#} seconds"));
        }
    }

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> of the body, or <see langword="null"/> when there is more.
    /// </summary>
    private static async Task<string?> BoundedBodyAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        // One byte over the limit is read on purpose: it is how "exactly at the limit" is told apart from "more
        // than the limit" without trusting a header.
        var buffer = new byte[maxBytes + 1];
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await stream
                .ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total > maxBytes ? null : Encoding.UTF8.GetString(buffer, 0, total);
    }

    /// <summary>
    /// A JSON scalar as text, or <see langword="null"/> when the node is not one.
    /// </summary>
    /// <remarks>
    /// Numbers and booleans included, because a registry writes a version as a number and a flag as a boolean
    /// often enough that dropping them would lose evidence a reader wants. Objects and arrays are not flattened:
    /// a report prints these, and a flattened tree printed as a flat list of keys reads as though the registry had
    /// sent it that way.
    /// </remarks>
    /// <param name="node">The node to read.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    internal static string? Scalar(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value when value.TryGetValue<bool>(out var flag) => flag ? "true" : "false",
        JsonValue value when value.TryGetValue<double>(out var number) =>
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };

    private static Exception Innermost(Exception exception)
    {
        var current = exception;

        while (current.InnerException is { } inner)
        {
            current = inner;
        }

        return current;
    }
}
