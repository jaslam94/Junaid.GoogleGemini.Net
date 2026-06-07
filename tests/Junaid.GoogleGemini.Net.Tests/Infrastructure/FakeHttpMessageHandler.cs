using System.Net;
using System.Text;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that lets a test control exactly what the
/// HTTP layer returns, while recording every request (and its body) for assertions.
///
/// This is the foundation for testing the client without hitting the real Gemini API. In Phase 1
/// it will also drive the retry tests (by returning 429/500 first, then 200) and the streaming
/// tests (by returning a canned SSE body).
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;

    /// <summary>Every request that passed through this handler, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>The captured request body string for each request (null when there was no body).</summary>
    public List<string?> RequestBodies { get; } = [];

    /// <summary>Number of times the handler was invoked — handy for asserting retry behavior.</summary>
    public int CallCount => Requests.Count;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((req, _, _) => Task.FromResult(responder(req)))
    {
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    /// <summary>Always returns the same status code and JSON body.</summary>
    public static FakeHttpMessageHandler RespondWith(HttpStatusCode status, string json) =>
        new(_ => JsonResponse(status, json));

    /// <summary>Returns each queued response in turn (then repeats the last) — useful for retry tests.</summary>
    public static FakeHttpMessageHandler Sequence(params HttpResponseMessage[] responses) =>
        new((_, attempt, _) => Task.FromResult(responses[Math.Min(attempt, responses.Length - 1)]));

    public static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var attempt = Requests.Count;
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return await _responder(request, attempt, cancellationToken);
    }
}
