using System.Net;
using System.Text;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// Returns a fixed, realistic <c>generateContent</c> response instantly, without touching the
/// network or disk. Used as the innermost transport for every benchmark so all of them measure
/// only this library's own code, never Gemini's real latency or Google's infrastructure.
/// </summary>
/// <remarks>
/// Always registered as the <em>primary</em> handler, replacing <see cref="HttpClientHandler"/>
/// outright rather than wrapping it. <see cref="RawHttpClientBenchmarks"/> passes it directly to
/// <c>new HttpClient(...)</c>, and <see cref="GeminiClientDefaultBenchmarks"/> /
/// <see cref="GeminiClientFullyObservedBenchmarks"/> install it via
/// <c>ConfigurePrimaryHttpMessageHandler</c> after <c>AddGemini</c> has already wired up the real
/// pipeline. That's the opposite end from <c>Junaid.GoogleGemini.Net.Testing</c>'s cassette
/// handler, which sits OUTERMOST (before auth) so a recording never sees the API key. This
/// handler needs to sit INNERMOST instead, so auth and the resilience/retry handler still run for
/// real and only the final network hop is faked.
/// </remarks>
public sealed class FakeGeminiHandler : DelegatingHandler
{
    /// <summary>
    /// A realistic <c>generateContent</c> response body: one candidate, a short text part, a
    /// normal STOP finish reason, and non-zero token usage so cost-governance and telemetry code
    /// paths (which only do real work when usage is present) actually execute in the benchmark.
    /// </summary>
    public const string ResponseJson = """
        {
          "candidates": [
            {
              "content": {
                "role": "model",
                "parts": [
                  { "text": "This is a canned benchmark response standing in for a real Gemini reply, long enough to exercise realistic string/JSON handling without hitting the network." }
                ]
              },
              "finishReason": "STOP",
              "index": 0
            }
          ],
          "usageMetadata": {
            "promptTokenCount": 12,
            "candidatesTokenCount": 28,
            "totalTokenCount": 40
          }
        }
        """;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
