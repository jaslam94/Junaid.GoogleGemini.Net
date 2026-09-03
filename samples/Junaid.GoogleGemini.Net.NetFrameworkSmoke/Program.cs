using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    private static async Task<int> Main()
    {
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");

        var key = Environment.GetEnvironmentVariable("GeminiApiKey");
        if (string.IsNullOrWhiteSpace(key))
        {
            // The library assembly loaded and the DI extension is callable — that alone proves the
            // netstandard2.0 build is usable on .NET Framework. Skip the network call.
            Console.WriteLine("GeminiApiKey not set; skipping live call. (ns2.0 assembly loaded OK.)");
            return 0;
        }

        var services = new ServiceCollection();
        services.AddGemini(options =>
        {
            options.ApiKey = key;
            options.DefaultModel = "gemini-3.8-flash";
        });

        using var provider = services.BuildServiceProvider();
        var gemini = provider.GetRequiredService<IGeminiService>();

        // Exercises the full ns2.0 runtime path on .NET Framework: HttpClient + the polyfilled
        // GeminiRetryHandler + System.Text.Json (source-gen) deserialization.
        var response = await gemini.GenerateAsync("Reply with exactly the word: pong");
        var text = response.Text();
        Console.WriteLine($"Live response: '{text}'  (finish={response.FinishReason})");

        return string.IsNullOrWhiteSpace(text) ? 1 : 0;
    }
}
