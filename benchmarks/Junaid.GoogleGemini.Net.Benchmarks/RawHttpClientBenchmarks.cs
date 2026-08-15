using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// Baseline: a bare <see cref="HttpClient"/> doing exactly one JSON POST + JSON parse, none of
/// this library's auth/retry/rate-limit/cost/telemetry layers. Stands in for "the minimum work
/// any Gemini client — hand-rolled or otherwise — has to do", so the other two benchmark classes
/// can be read as "how much does <c>Junaid.GoogleGemini.Net</c> add on top of this".
/// </summary>
[MemoryDiagnoser]
public class RawHttpClientBenchmarks
{
    private HttpClient _httpClient = null!;
    private GenerateContentRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient(new FakeGeminiHandler())
        {
            BaseAddress = new Uri("https://benchmark.invalid/v1beta/"),
        };
        _request = RequestFactory.CreateTextRequest(BenchmarkPrompt.Text);
    }

    [GlobalCleanup]
    public void Cleanup() => _httpClient.Dispose();

    [Benchmark(Baseline = true)]
    public async Task<string> TextGeneration()
    {
        var json = JsonSerializer.Serialize(_request, GeminiJson.Default);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("models/gemini-3.7-flash:generateContent", content);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GenerateContentResponse>(body, GeminiJson.Default);
        return result!.Text();
    }
}
