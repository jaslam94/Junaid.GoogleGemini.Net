namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// Shared fixtures every benchmark class sends/targets, so all three measure the same request
/// shape against the same (fake) endpoint. Centralized here rather than retyped per class so they
/// can't quietly drift apart. <c>.invalid</c> is the IANA-reserved TLD for exactly this "will
/// never resolve, isn't a real host" use.
/// </summary>
internal static class BenchmarkFixtures
{
    public const string Text = "Explain, in two sentences, how token-bucket rate limiting interacts with exponential backoff retries in a resilient HTTP client.";

    public const string BaseUrl = "https://benchmark.invalid/v1beta/";
}
