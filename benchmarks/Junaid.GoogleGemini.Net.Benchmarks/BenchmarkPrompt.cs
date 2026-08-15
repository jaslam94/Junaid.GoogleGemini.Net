namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>The one prompt every benchmark class sends, so all three measure the same request shape.</summary>
internal static class BenchmarkPrompt
{
    public const string Text = "Explain, in two sentences, how token-bucket rate limiting interacts with exponential backoff retries in a resilient HTTP client.";
}
