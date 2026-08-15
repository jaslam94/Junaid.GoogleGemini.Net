using System.Runtime.CompilerServices;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Extensions.AI;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1) Register Gemini from the "Gemini" config section.
//    Provide the key via Gemini:ApiKey in appsettings, user-secrets, or the GeminiApiKey env var.
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));

// 2) Also expose Gemini through the Microsoft.Extensions.AI IChatClient abstraction.
builder.Services.AddGeminiChatClient(GeminiConstants.Models.Gemini37Flash);

// 3) Wire the library's OpenTelemetry traces + metrics to the console exporter so you can see
//    per-call spans (model, token counts, finish reason) and the duration/token histograms.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(GeminiTelemetry.SourceName).AddConsoleExporter())
    .WithMetrics(metrics => metrics.AddMeter(GeminiTelemetry.SourceName).AddConsoleExporter());

var app = builder.Build();

// Plain text generation:  GET /generate?prompt=Write%20a%20haiku
// Also demonstrates cost governance (see appsettings.json's Gemini:Budget section): a daily USD
// ceiling and a per-request estimate ceiling are both configured, so either can reject this call
// before it's sent.
app.MapGet("/generate", async (IGeminiService gemini, string prompt) =>
{
    try
    {
        return Results.Ok((await gemini.GenerateAsync(prompt, Fast())).Text());
    }
    catch (GeminiBudgetExceededException ex)
    {
        // Today's (UTC) cumulative real spend already reached Gemini:Budget:MaxCostPerDayUsd.
        return Results.Problem(
            $"Daily budget exceeded: ${ex.CurrentSpendUsd:F4} spent of ${ex.BudgetLimitUsd:F2}.",
            statusCode: StatusCodes.Status402PaymentRequired);
    }
    catch (GeminiRequestCostExceededException ex)
    {
        // This single call's estimated cost exceeded Gemini:Budget:MaxCostPerRequestUsd,
        // checked via a pre-flight CountTokensAsync call before the real request was sent.
        return Results.Problem(
            $"Estimated cost ${ex.EstimatedCostUsd:F4} exceeds the per-request ceiling of ${ex.MaxCostPerRequestUsd:F2}.",
            statusCode: StatusCodes.Status402PaymentRequired);
    }
});

// Today's cumulative spend so far, as tracked by the cost governor (in-memory, per process).
// GET /spend
app.MapGet("/spend", (ICostGovernor costGovernor) =>
    Results.Ok(new { TodaySpendUsd = costGovernor.GetTodaySpend() }));

// Typed structured output: GET /recipe?dish=pancakes  -> JSON shaped like Recipe
app.MapGet("/recipe", async (IGeminiService gemini, string dish) =>
    Results.Ok(await gemini.GenerateAsync<Recipe>($"A simple recipe for {dish}.", Fast())));

// Streaming: GET /stream?prompt=...  -> chunks streamed as they arrive
app.MapGet("/stream", (IGeminiService gemini, string prompt, CancellationToken ct)
    => StreamText(gemini, prompt, ct));

// Image generation: GET /image?prompt=a%20red%20fox  -> the generated image bytes
app.MapGet("/image", async (IGeminiService gemini, string prompt) =>
{
    var response = await gemini.GenerateImageAsync(prompt);
    return response.TryGetImages(out var images)
        ? Results.File(images[0].Data, images[0].MimeType)
        : Results.Problem("The model returned no image.", statusCode: 502);
});

// Chat via Microsoft.Extensions.AI: POST /chat  { "message": "Hello" }
app.MapPost("/chat", async (IChatClient chat, ChatRequest request) =>
    Results.Ok((await chat.GetResponseAsync([new ChatMessage(ChatRole.User, request.Message)])).Text));

app.Run();

// gemini-3.7-flash (like the 3.x flash models before it) defaults to a "high" thinking level, which
// can add tens of seconds (even >2 min) of latency per call. For these simple demo endpoints we opt
// down to "low" so responses are snappy.
// Drop this (or raise it) when you actually want deeper reasoning.
static GeminiRequestOptions Fast() => new() { ThinkingLevel = GeminiConstants.ThinkingLevels.Low };

static async IAsyncEnumerable<string> StreamText(
    IGeminiService gemini, string prompt, [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var chunk in gemini.StreamAsync(prompt, Fast(), ct))
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
