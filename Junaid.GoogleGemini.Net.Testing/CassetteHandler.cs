using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>
/// Delegating handler that records real HTTP exchanges to a cassette file and replays them on
/// later runs, so tests against the Gemini API can be deterministic, offline, and free.
/// </summary>
/// <remarks>
/// Register this as the OUTERMOST handler on the Gemini <see cref="HttpClient"/> (see
/// <see cref="GeminiTestingExtensions.AddCassette"/>) — outermost means it runs before
/// <c>GeminiAuthHandler</c> attaches the API key, so a recorded cassette can never contain a
/// secret, and replay never needs one either.
/// </remarks>
internal sealed class CassetteHandler : DelegatingHandler
{
    private readonly string _path;
    private readonly CassetteMode _mode;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CassetteFile? _cassette;
    private bool[]? _consumed;

    public CassetteHandler(string path, CassetteMode mode)
    {
        _path = path;
        _mode = mode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_mode == CassetteMode.Off)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var incoming = new CassetteRequest
        {
            Method = request.Method.Method,
            Uri = request.RequestUri?.PathAndQuery ?? "",
            Body = await BufferRequestBodyAsync(request, cancellationToken).ConfigureAwait(false),
        };

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            if (_mode != CassetteMode.Record)
            {
                var matchIndex = FindUnconsumedMatch(incoming);
                if (matchIndex >= 0)
                {
                    _consumed![matchIndex] = true;
                    return BuildResponse(_cassette!.Interactions[matchIndex].Response);
                }

                if (_mode == CassetteMode.Replay)
                {
                    throw new CassetteException(
                        $"No recorded interaction matches {incoming.Method} {incoming.Uri} in cassette '{_path}'. " +
                        "Re-run with CassetteMode.RecordOnce (and a real API key) to capture it.");
                }

                // RecordOnce with no match: fall through and hit the real API below.
            }

            using var liveResponse = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var recordedResponse = await CaptureResponseAsync(liveResponse, cancellationToken).ConfigureAwait(false);

            _cassette!.Interactions.Add(new CassetteInteraction { Request = incoming, Response = recordedResponse });
            _consumed = [.. _consumed!, true];
            await CassetteStore.SaveAsync(_path, _cassette, cancellationToken).ConfigureAwait(false);

            return BuildResponse(recordedResponse);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cassette is not null)
        {
            return;
        }

        _cassette = await CassetteStore.LoadAsync(_path, cancellationToken).ConfigureAwait(false);
        _consumed = new bool[_cassette.Interactions.Count];
    }

    /// <summary>
    /// Reads the request body for matching/recording, then replaces it with a fresh,
    /// re-readable copy — <see cref="HttpContent"/> can normally only be read once, and on a
    /// Record/RecordOnce pass the real send still needs to happen after we've read it here.
    /// </summary>
    private static async Task<string?> BufferRequestBodyAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return null;
        }

        var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = request.Content.Headers.ContentType;

        request.Content = new ByteArrayContent(bytes);
        if (contentType is not null)
        {
            request.Content.Headers.ContentType = contentType;
        }

        return bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes);
    }

    private static async Task<CassetteResponse> CaptureResponseAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new CassetteResponse
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            Body = Encoding.UTF8.GetString(bytes),
        };
    }

    private static HttpResponseMessage BuildResponse(CassetteResponse recorded)
    {
        var message = new HttpResponseMessage((HttpStatusCode)recorded.StatusCode)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(recorded.Body)),
        };
        if (recorded.ContentType is not null)
        {
            message.Content.Headers.TryAddWithoutValidation("Content-Type", recorded.ContentType);
        }
        return message;
    }

    /// <summary>
    /// Finds the earliest not-yet-consumed interaction matching method + endpoint + body, so
    /// repeated identical requests replay in recorded order rather than always the same one.
    /// </summary>
    private int FindUnconsumedMatch(CassetteRequest incoming)
    {
        for (var i = 0; i < _cassette!.Interactions.Count; i++)
        {
            if (_consumed![i])
            {
                continue;
            }

            var recorded = _cassette.Interactions[i].Request;
            if (!string.Equals(recorded.Method, incoming.Method, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!string.Equals(recorded.Uri, incoming.Uri, StringComparison.Ordinal))
            {
                continue;
            }
            if (BodiesMatch(recorded.Body, incoming.Body))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Compares bodies by JSON structure, not raw text — the file stores pretty-printed JSON for
    /// readability, while a live request body is compact, so an ordinal comparison would always
    /// mismatch even for identical requests.
    /// </summary>
    private static bool BodiesMatch(string? recorded, string? incoming)
    {
        if (string.IsNullOrEmpty(recorded) && string.IsNullOrEmpty(incoming))
        {
            return true;
        }
        if (string.IsNullOrEmpty(recorded) || string.IsNullOrEmpty(incoming))
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(recorded), JsonNode.Parse(incoming));
        }
        catch (JsonException)
        {
            return string.Equals(recorded, incoming, StringComparison.Ordinal);
        }
    }
}
