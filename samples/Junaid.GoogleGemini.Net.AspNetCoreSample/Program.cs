using System.Runtime.CompilerServices;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Extensions.AI;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1) Register Gemini from the "Gemini" config section.
//    Provide the key via Gemini:ApiKey in appsettings, user-secrets, or the GeminiApiKey env var.
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));

// 2) Also expose Gemini through the Microsoft.Extensions.AI IChatClient abstraction.
builder.Services.AddGeminiChatClient("gemini-2.5-flash");

// 3) Wire the library's OpenTelemetry traces + metrics to the console exporter so you can see
//    per-call spans (model, token counts, finish reason) and the duration/token histograms.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(GeminiTelemetry.SourceName).AddConsoleExporter())
    .WithMetrics(metrics => metrics.AddMeter(GeminiTelemetry.SourceName).AddConsoleExporter());

var app = builder.Build();

// Plain text generation:  GET /generate?prompt=Write%20a%20haiku
app.MapGet("/generate", async (IGeminiService gemini, string prompt) =>
    Results.Ok((await gemini.GenerateAsync(prompt)).Text()));

// Typed structured output: GET /recipe?dish=pancakes  -> JSON shaped like Recipe
app.MapGet("/recipe", async (IGeminiService gemini, string dish) =>
    Results.Ok(await gemini.GenerateAsync<Recipe>($"A simple recipe for {dish}.")));

// Streaming: GET /stream?prompt=...  -> chunks streamed as they arrive
app.MapGet("/stream", (IGeminiService gemini, string prompt, CancellationToken ct)
    => StreamText(gemini, prompt, ct));

// Chat via Microsoft.Extensions.AI: POST /chat  { "message": "Hello" }
app.MapPost("/chat", async (IChatClient chat, ChatRequest request) =>
    Results.Ok((await chat.GetResponseAsync([new ChatMessage(ChatRole.User, request.Message)])).Text));

app.Run();

static async IAsyncEnumerable<string> StreamText(
    IGeminiService gemini, string prompt, [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var chunk in gemini.StreamAsync(prompt, cancellationToken: ct))
    {
        var text = chunk.Text();
        if (!string.IsNullOrEmpty(text))
        {
            yield return text;
        }
    }
}

internal record Recipe(string Title, string[] Ingredients, int Minutes);
internal record ChatRequest(string Message);
