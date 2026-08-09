using System.Diagnostics;
using System.Diagnostics.Metrics;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry-native tracing and metrics for Gemini calls, following the OTel GenAI semantic
/// conventions. There is <b>no</b> dependency on any OpenTelemetry package — this uses only
/// <see cref="System.Diagnostics"/> from the BCL. Consumers opt in by subscribing to the source/meter:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(GeminiTelemetry.SourceName))
///     .WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName));
/// </code>
/// If nobody listens, the overhead is negligible (activities aren't created, records are no-ops).
/// </summary>
public static class GeminiTelemetry
{
    /// <summary>The ActivitySource / Meter name to subscribe to.</summary>
    public const string SourceName = "Junaid.GoogleGemini.Net";

    internal const string SystemName = "gemini";
    private const string Version = "6.2.0";

    internal static readonly ActivitySource ActivitySource = new(SourceName, Version);
    internal static readonly Meter Meter = new(SourceName, Version);

    internal static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "gen_ai.client.operation.duration", unit: "s", description: "Duration of a Gemini client operation.");

    internal static readonly Histogram<long> TokenUsage = Meter.CreateHistogram<long>(
        "gen_ai.client.token.usage", unit: "{token}", description: "Number of tokens used.");

    // Not "gen_ai.client.cost.*": as of this writing there is no official OTel GenAI semconv cost
    // metric, so reusing that prefix would falsely imply standards compliance. This uses the
    // library's own "gemini" namespace instead (see GeminiTelemetry.SystemName). Revisit if/when an
    // official semconv cost attribute is standardized.
    internal static readonly Counter<double> CostUsd = Meter.CreateCounter<double>(
        "gemini.client.cost.usd", unit: "USD", description: "Cumulative USD cost of Gemini client operations.");

    /// <summary>Starts a client span for an operation, tagged per the GenAI conventions.</summary>
    internal static Activity? StartOperation(string operation, string? model)
    {
        var name = string.IsNullOrEmpty(model) ? operation : $"{operation} {model}";
        var activity = ActivitySource.StartActivity(name, ActivityKind.Client);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("gen_ai.system", SystemName);
            activity.SetTag("gen_ai.operation.name", operation);
            if (model is not null)
            {
                activity.SetTag("gen_ai.request.model", model);
            }
        }
        return activity;
    }

    /// <summary>Records token-usage metrics and span tags from a response.</summary>
    internal static void RecordUsage(string operation, string? model, UsageMetadata? usage, string? finishReason, Activity? activity)
    {
        if (usage is null)
        {
            return;
        }

        var baseTags = new TagList { { "gen_ai.system", SystemName }, { "gen_ai.operation.name", operation } };
        if (model is not null)
        {
            baseTags.Add("gen_ai.request.model", model);
        }

        var inputTags = baseTags;
        inputTags.Add("gen_ai.token.type", "input");
        TokenUsage.Record(usage.PromptTokenCount, inputTags);

        var outputTags = baseTags;
        outputTags.Add("gen_ai.token.type", "output");
        TokenUsage.Record(usage.CandidatesTokenCount, outputTags);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("gen_ai.usage.input_tokens", usage.PromptTokenCount);
            activity.SetTag("gen_ai.usage.output_tokens", usage.CandidatesTokenCount);
            if (finishReason is not null)
            {
                activity.SetTag("gen_ai.response.finish_reasons", new[] { finishReason });
            }
        }
    }

    /// <summary>Records the <c>gemini.client.cost.usd</c> cost metric for one call's real usage.</summary>
    internal static void RecordCost(string? model, decimal costUsd)
    {
        var tags = new TagList { { "gen_ai.system", SystemName } };
        if (model is not null)
        {
            tags.Add("gen_ai.request.model", model);
        }
        // Counter<T>.Add, not .Record: a Counter is additive (cumulative spend), unlike the
        // Histogram<T>s above which record a distribution of individual values.
        CostUsd.Add((double)costUsd, tags);
    }

    /// <summary>Records the operation-duration metric (seconds).</summary>
    internal static void RecordDuration(string operation, string? model, double seconds)
    {
        var tags = new TagList { { "gen_ai.system", SystemName }, { "gen_ai.operation.name", operation } };
        if (model is not null)
        {
            tags.Add("gen_ai.request.model", model);
        }
        OperationDuration.Record(seconds, tags);
    }

    /// <summary>
    /// Parses an endpoint like <c>models/gemini-2.5-pro:generateContent?alt=sse</c> into the operation
    /// name (<c>generateContent</c>) and model (<c>gemini-2.5-pro</c>).
    /// </summary>
    internal static (string Operation, string? Model) Parse(string endpoint)
    {
        string operation = endpoint;
        string? model = null;

        var colon = endpoint.IndexOf(':');
        if (colon >= 0)
        {
            operation = endpoint[(colon + 1)..];
            var query = operation.IndexOf('?');
            if (query >= 0)
            {
                operation = operation[..query];
            }

            var path = endpoint[..colon];
            if (path.StartsWith("models/", StringComparison.Ordinal))
            {
                model = path["models/".Length..];
            }
        }

        return (operation, model);
    }
}
