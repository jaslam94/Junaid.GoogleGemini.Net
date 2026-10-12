# Observability (OpenTelemetry)

The library emits traces and metrics via `System.Diagnostics`, following the OpenTelemetry GenAI
semantic conventions. The library has no OpenTelemetry package dependency. You opt in by subscribing
to the source and meter. If nobody listens, the overhead is negligible.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(GeminiTelemetry.SourceName))
    .WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName));
```

`GeminiTelemetry.SourceName` is `"Junaid.GoogleGemini.Net"`.

## What you get

Each API call produces one span, tagged with:

- `gen_ai.system` = `gemini`
- `gen_ai.operation.name` (e.g. `generateContent`)
- `gen_ai.request.model`
- `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`
- `gen_ai.response.finish_reasons`
- error status on failures

The library records these metrics:

- `gen_ai.client.operation.duration` (histogram, seconds)
- `gen_ai.client.token.usage` (histogram, tagged `gen_ai.token.type` = input/output)
- `gemini.client.cost.usd` (counter), recorded for every priced call whether or not budget
  enforcement is enabled. The name does not start with `gen_ai.` because OpenTelemetry has no GenAI
  semantic convention for cost. See [Cost governance](cost-governance.md).

The token metrics report `promptTokenCount` and the output tokens. An agentic video call
(`Part.MediaProcessing = "AGENTIC"`) reports its video tokens as `toolUsePromptTokenCount`, so the
token metrics under-report it. The cost metric includes those tokens. See
[Cost governance](cost-governance.md).

Streaming calls (`StreamAsync`/`StreamChatAsync`) are instrumented the same way as non-streaming
ones, including spans and all three metrics above.

Batch API calls are not instrumented at all. They emit no spans and no duration, token, or cost
metrics. `IBatchService` uses its own `HttpClient` instead of the shared `GeminiClient` pipeline that
emits the data above (see [Batch API](batch-api.md)), so none of it applies there today.

## Exporting

Add any OpenTelemetry exporter, such as console, OTLP, or Azure Monitor. For example, the console exporter
(used by the ASP.NET Core sample):

```csharp
.WithTracing(t => t.AddSource(GeminiTelemetry.SourceName).AddConsoleExporter())
.WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName).AddConsoleExporter());
```
