using System.Net;
using System.Text;

namespace Junaid.GoogleGemini.Net.Testing.Tests;

/// <summary>
/// Stands in for the real network call, innermost in the pipeline. Used to assert whether the
/// "live API" was actually reached, and what it received.
/// </summary>
internal sealed class FakeInnerHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public int CallCount { get; private set; }
    public List<string?> RequestBodies { get; } = [];

    public FakeInnerHandler(params HttpResponseMessage[] responses) => _responses = new(responses);

    public static FakeInnerHandler Json(HttpStatusCode status, string json) =>
        new(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    /// <summary>A handler that fails the test if the "live API" is ever reached.</summary>
    public static FakeInnerHandler MustNotBeCalled() => new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                "The live API was reached but this test expected it never to be (replay should have short-circuited).");
        }

        return _responses.Dequeue();
    }
}
